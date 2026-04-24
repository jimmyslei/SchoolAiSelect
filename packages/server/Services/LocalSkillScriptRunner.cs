using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SchoolRecommender.Api.Options;

namespace SchoolRecommender.Api.Services;

public sealed class LocalSkillScriptRunner(SchoolServiceOptions options, ILogger<LocalSkillScriptRunner> logger)
{
    private readonly string skillDebugLogPath = Path.Combine(
        Path.GetDirectoryName(options.SkillRootPath) ?? Directory.GetCurrentDirectory(),
        "logs",
        "skill-debug.log");

    public Task<object?> RunAsync(
        AgentFileSkill skill,
        AgentFileSkillScript script,
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "File skill invoked. Skill: {SkillName}, Script: {ScriptName}, Arguments: {Arguments}",
            skill.Frontmatter.Name,
            script.Name,
            SerializeArguments(arguments));

        var argumentMap = ToArgumentDictionary(arguments);

        WriteSkillDebugLog(
            "FILE_SKILL_INVOKED",
            $"Skill={skill.Frontmatter.Name}; Script={script.Name}; Arguments={SerializeArguments(argumentMap)}");
        return ExecuteScriptAsync(skill.Frontmatter.Name, script.Name, script.FullPath, argumentMap, cancellationToken);
    }

    // ─── 显式工具入口（供 AIFunctionFactory 直接注册，带明确参数 schema）────────────

    /// <summary>
    /// 搜索微信公众号文章。调用 skills/wechat-article-search/scripts/search_wechat.js。
    /// </summary>
    public async Task<string> SearchWechatArticlesAsync(string query, int? num, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Error: query 不能为空";
        }

        var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["query"] = query.Trim(),
        };
        if (num is > 0)
        {
            arguments["num"] = num.Value;
        }

        var scriptName = Path.Combine("scripts", "search_wechat.js");
        var scriptFullPath = Path.Combine(options.SkillRootPath, "wechat-article-search", scriptName);

        logger.LogInformation(
            "Explicit tool invoked. Skill: wechat-article-search, Arguments: {Arguments}",
            SerializeArguments(arguments));
        WriteSkillDebugLog(
            "EXPLICIT_TOOL_INVOKED",
            $"Tool=search_wechat_articles; Script={scriptName}; Arguments={SerializeArguments(arguments)}");

        var result = await ExecuteScriptAsync(
            "wechat-article-search",
            scriptName.Replace('\\', '/'),
            scriptFullPath,
            arguments,
            cancellationToken).ConfigureAwait(false);

        return result?.ToString() ?? "(no output)";
    }

    /// <summary>
    /// 搜索小红书笔记。调用 skills/xiaohongshu-search scripts 包的 search 子命令。
    /// </summary>
    public async Task<string> SearchXiaohongshuNotesAsync(string keyword, int? limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return "Error: keyword 不能为空";
        }

        var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["keyword"] = keyword.Trim(),
        };
        if (limit is > 0)
        {
            arguments["limit"] = limit.Value;
        }

        var scriptName = Path.Combine("scripts", "search.py");
        var scriptFullPath = Path.Combine(options.SkillRootPath, "xiaohongshu-search", scriptName);

        logger.LogInformation(
            "Explicit tool invoked. Skill: xiaohongshu-search, Arguments: {Arguments}",
            SerializeArguments(arguments));
        WriteSkillDebugLog(
            "EXPLICIT_TOOL_INVOKED",
            $"Tool=search_xiaohongshu_notes; Script={scriptName}; Arguments={SerializeArguments(arguments)}");

        var result = await ExecuteScriptAsync(
            "xiaohongshu-search",
            scriptName.Replace('\\', '/'),
            scriptFullPath,
            arguments,
            cancellationToken).ConfigureAwait(false);

        return result?.ToString() ?? "(no output)";
    }
    private async Task<object?> ExecuteScriptAsync(
        string skillName,
        string scriptName,
        string scriptFullPath,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Executing skill script. Skill: {SkillName}, Script: {ScriptName}, Path: {ScriptPath}, Arguments: {Arguments}",
            skillName,
            scriptName,
            scriptFullPath,
            SerializeArguments(arguments));
        if (!File.Exists(scriptFullPath))
        {
            WriteSkillDebugLog(
                "SCRIPT_MISSING",
                $"Skill={skillName}; Script={scriptName}; Path={scriptFullPath}; Arguments={SerializeArguments(arguments)}");
            return $"Error: Script file not found: {scriptFullPath}";
        }

        try
        {
            var startInfo = BuildProcessStartInfo(scriptName, scriptFullPath, arguments);
            var renderedCommand = RenderCommand(startInfo);

            WriteSkillDebugLog(
                "SCRIPT_EXECUTING",
                $"Skill={skillName}; Script={scriptName}; Path={scriptFullPath}; WorkingDirectory={startInfo.WorkingDirectory}; Command={renderedCommand}; Arguments={SerializeArguments(arguments)}");

            using var process = new Process { StartInfo = startInfo };

            process.Start();

            var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var exitTask = process.WaitForExitAsync(cancellationToken);
            var timeoutTask = Task.Delay(options.SkillTimeout, cancellationToken);

            var completedTask = await Task.WhenAny(Task.WhenAll(stdOutTask, stdErrTask, exitTask), timeoutTask).ConfigureAwait(false);
            if (completedTask == timeoutTask)
            {
                TryKill(process);
                WriteSkillDebugLog(
                    "SCRIPT_TIMEOUT",
                    $"Skill={skillName}; Script={scriptName}; Command={renderedCommand}; TimeoutSeconds={options.SkillTimeout.TotalSeconds:0}; Arguments={SerializeArguments(arguments)}");
                return $"Error: Script '{scriptName}' timed out after {options.SkillTimeout.TotalSeconds:0} seconds.";
            }

            var stdOut = await stdOutTask.ConfigureAwait(false);
            var stdErr = await stdErrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                logger.LogWarning(
                    "Skill script failed. Skill: {SkillName}, Script: {ScriptName}, Error: {Error}",
                    skillName,
                    scriptName,
                    stdErr);

                WriteSkillDebugLog(
                    "SCRIPT_FAILED",
                    $"Skill={skillName}; Script={scriptName}; ExitCode={process.ExitCode}; Command={renderedCommand}; Arguments={SerializeArguments(arguments)}; StdOut={TruncateForDebugLog(stdOut)}; StdErr={TruncateForDebugLog(stdErr)}");

                return string.IsNullOrWhiteSpace(stdErr)
                    ? $"Error: Script '{scriptName}' exited with code {process.ExitCode}."
                    : $"Error: {stdErr.Trim()}";
            }

            var output = string.IsNullOrWhiteSpace(stdOut) ? stdErr : stdOut;
            var normalizedOutput = string.IsNullOrWhiteSpace(output) ? "(no output)" : output.Trim();

            logger.LogInformation(
                "Skill script completed. Skill: {SkillName}, Script: {ScriptName}, Arguments: {Arguments}, Result: {Result}",
                skillName,
                scriptName,
                SerializeArguments(arguments),
                TruncateForLog(normalizedOutput));

            WriteSkillDebugLog(
                "SCRIPT_COMPLETED",
                $"Skill={skillName}; Script={scriptName}; ExitCode={process.ExitCode}; Command={renderedCommand}; Arguments={SerializeArguments(arguments)}; StdOut={TruncateForDebugLog(stdOut)}; StdErr={TruncateForDebugLog(stdErr)}; Result={TruncateForDebugLog(normalizedOutput)}");

            return normalizedOutput;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Skill script execution failed. Skill: {SkillName}, Script: {ScriptName}", skillName, scriptName);
            WriteSkillDebugLog(
                "SCRIPT_EXCEPTION",
                $"Skill={skillName}; Script={scriptName}; Path={scriptFullPath}; Arguments={SerializeArguments(arguments)}; Exception={exception}");
            return $"Error: Failed to execute script '{scriptName}': {exception.Message}";
        }
    }

    private ProcessStartInfo BuildProcessStartInfo(
        string scriptName,
        string scriptFullPath,
        IReadOnlyDictionary<string, object?> arguments)
    {
        var extension = Path.GetExtension(scriptFullPath).ToLowerInvariant();

        return extension switch
        {
            ".js" => BuildJavaScriptStartInfo(scriptName, scriptFullPath, arguments),
            ".py" => BuildPythonStartInfo(scriptName, scriptFullPath, arguments),
            _ => BuildGenericStartInfo(scriptName, scriptFullPath, arguments),
        };
    }

    private ProcessStartInfo BuildJavaScriptStartInfo(
        string scriptName,
        string scriptFullPath,
        IReadOnlyDictionary<string, object?> arguments)
    {
        var skillRoot = GetSkillRoot(scriptFullPath);
        var startInfo = CreateStartInfo(options.NodeCommand, skillRoot);
        startInfo.ArgumentList.Add(GetScriptInvocationPath(scriptFullPath, skillRoot));
        AppendScriptArguments(startInfo.ArgumentList, scriptName, arguments);

        return startInfo;
    }

    private ProcessStartInfo BuildPythonStartInfo(
        string scriptName,
        string scriptFullPath,
        IReadOnlyDictionary<string, object?> arguments)
    {
        var skillRoot = GetSkillRoot(scriptFullPath);
        var startInfo = ShouldRunPythonAsModule(scriptFullPath)
            ? CreateStartInfo(options.PythonCommand, skillRoot)
            : CreateStartInfo(options.PythonCommand, Path.GetDirectoryName(scriptFullPath) ?? skillRoot);

        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";

        if (ShouldRunPythonAsModule(scriptFullPath))
        {
            var moduleName = Path.GetFileName(Path.GetDirectoryName(scriptFullPath)) ?? "scripts";
            startInfo.ArgumentList.Add("-m");
            startInfo.ArgumentList.Add(moduleName);

            var scriptStem = Path.GetFileNameWithoutExtension(scriptName);
            if (!string.Equals(scriptStem, "__main__", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.ArgumentList.Add(scriptStem);
            }
        }
        else
        {
            startInfo.ArgumentList.Add(scriptFullPath);
        }

        AppendScriptArguments(startInfo.ArgumentList, scriptName, arguments);

        return startInfo;
    }

    private ProcessStartInfo BuildGenericStartInfo(
        string scriptName,
        string scriptFullPath,
        IReadOnlyDictionary<string, object?> arguments)
    {
        var extension = Path.GetExtension(scriptFullPath).ToLowerInvariant();
        var interpreter = extension switch
        {
            ".py" => options.PythonCommand,
            ".js" => options.NodeCommand,
            ".ps1" => "pwsh",
            _ => null,
        };

        var workingDirectory = Path.GetDirectoryName(scriptFullPath) ?? Directory.GetCurrentDirectory();
        var startInfo = CreateStartInfo(interpreter ?? scriptFullPath, workingDirectory);

        if (interpreter is not null)
        {
            startInfo.ArgumentList.Add(scriptFullPath);
        }

        AppendScriptArguments(startInfo.ArgumentList, scriptName, arguments);

        return startInfo;
    }

    private static bool ShouldRunPythonAsModule(string scriptFullPath)
    {
        var scriptDirectory = Path.GetDirectoryName(scriptFullPath);
        if (scriptDirectory is null)
        {
            return false;
        }

        return File.Exists(Path.Combine(scriptDirectory, "__main__.py"))
            && File.Exists(Path.Combine(scriptDirectory, "__init__.py"));
    }

    private static string GetScriptInvocationPath(string scriptFullPath, string workingDirectory)
    {
        var relativePath = Path.GetRelativePath(workingDirectory, scriptFullPath);
        return string.IsNullOrWhiteSpace(relativePath) ? scriptFullPath : relativePath;
    }

    private static void AppendScriptArguments(
        Collection<string> arguments,
        string scriptName,
        IReadOnlyDictionary<string, object?> values)
    {
        var scriptStem = Path.GetFileNameWithoutExtension(scriptName);
        var positionalArguments = GetExplicitPositionalArguments(values).ToList();

        var usedCompatibility = false;
        if (positionalArguments.Count == 0)
        {
            var compatibilityArgs = GetCompatibilityPositionalArguments(scriptStem, values).ToList();
            if (compatibilityArgs.Count > 0)
            {
                positionalArguments.AddRange(compatibilityArgs);
                usedCompatibility = true;
            }
        }

        // 如果没有任何位置参数，则将所有非保留参数的值作为位置参数传递
        if (positionalArguments.Count == 0)
        {
            foreach (var (key, value) in values)
            {
                if (IsReservedArgument(key) || value is null)
                {
                    continue;
                }

                foreach (var item in EnumerateArgumentValues(value))
                {
                    arguments.Add(item);
                }
            }

            return;
        }

        foreach (var positionalArgument in positionalArguments)
        {
            arguments.Add(positionalArgument);
        }

        // 已有位置参数时，跳过剩余 key-value 参数（兼容性模式或显式位置参数已覆盖所有输入）
        if (usedCompatibility || GetExplicitPositionalArguments(values).Any())
        {
            return;
        }

        foreach (var (key, value) in values)
        {
            if (IsReservedArgument(key) || IsConsumedCompatibilityArgument(scriptStem, key))
            {
                continue;
            }

            AppendArgument(arguments, key, value);
        }
    }

    private static string GetSkillRoot(string scriptFullPath)
    {
        var scriptDirectory = Path.GetDirectoryName(scriptFullPath) ?? Directory.GetCurrentDirectory();
        return Path.GetDirectoryName(scriptDirectory) ?? scriptDirectory;
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, string workingDirectory) =>
        new()
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

    private static string? GetStringArgument(IReadOnlyDictionary<string, object?> arguments, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryGetArgument(arguments, key, out var value) && value is not null)
            {
                var text = value.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static bool TryGetArgument(IReadOnlyDictionary<string, object?> arguments, string key, out object? value)
    {
        return arguments.TryGetValue(key, out value);
    }

    private static IEnumerable<string> GetExplicitPositionalArguments(IReadOnlyDictionary<string, object?> arguments)
    {
        foreach (var reservedKey in ReservedArgumentKeys)
        {
            if (!TryGetArgument(arguments, reservedKey, out var value) || value is null)
            {
                continue;
            }

            foreach (var item in EnumerateArgumentValues(value))
            {
                yield return item;
            }

            yield break;
        }
    }

    private static IEnumerable<string> GetCompatibilityPositionalArguments(string scriptStem, IReadOnlyDictionary<string, object?> arguments)
    {
        return scriptStem switch
        {
            "search_wechat" => GetPositionalValues(arguments, "query", "keyword", "schoolName"),
            "search" => GetPositionalValues(arguments, "keyword", "query", "schoolName"),
            "feed" => GetPositionalValues(arguments, "feed_id", "feedId", "id", "xsec_token", "xsecToken"),
            "user" => GetPositionalValues(arguments, "user_id", "userId", "id", "xsec_token", "xsecToken"),
            _ => [],
        };
    }

    private static IEnumerable<string> GetPositionalValues(IReadOnlyDictionary<string, object?> arguments, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (GetStringArgument(arguments, key) is { Length: > 0 } value)
            {
                yield return value;
            }
        }
    }

    private static bool IsConsumedCompatibilityArgument(string scriptStem, string key)
    {
        return scriptStem switch
        {
            "search_wechat" => key.Equals("query", StringComparison.OrdinalIgnoreCase)
                || key.Equals("keyword", StringComparison.OrdinalIgnoreCase)
                || key.Equals("schoolName", StringComparison.OrdinalIgnoreCase),
            "search" => key.Equals("keyword", StringComparison.OrdinalIgnoreCase)
                || key.Equals("query", StringComparison.OrdinalIgnoreCase)
                || key.Equals("schoolName", StringComparison.OrdinalIgnoreCase),
            "feed" => key.Equals("feed_id", StringComparison.OrdinalIgnoreCase)
                || key.Equals("feedId", StringComparison.OrdinalIgnoreCase)
                || key.Equals("id", StringComparison.OrdinalIgnoreCase)
                || key.Equals("xsec_token", StringComparison.OrdinalIgnoreCase)
                || key.Equals("xsecToken", StringComparison.OrdinalIgnoreCase),
            "user" => key.Equals("user_id", StringComparison.OrdinalIgnoreCase)
                || key.Equals("userId", StringComparison.OrdinalIgnoreCase)
                || key.Equals("id", StringComparison.OrdinalIgnoreCase)
                || key.Equals("xsec_token", StringComparison.OrdinalIgnoreCase)
                || key.Equals("xsecToken", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static IEnumerable<string> EnumerateArgumentValues(object value)
    {
        if (value is string text)
        {
            yield return text;
            yield break;
        }

        if (value is JsonElement jsonElement)
        {
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.Array:
                    foreach (var item in jsonElement.EnumerateArray())
                    {
                        yield return item.ToString();
                    }

                    yield break;

                case JsonValueKind.String:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    yield return jsonElement.ToString();
                    yield break;
            }
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is not null)
                {
                    yield return item.ToString() ?? string.Empty;
                }
            }

            yield break;
        }

        yield return value.ToString() ?? string.Empty;
    }

    private static bool IsReservedArgument(string key)
    {
        return ReservedArgumentKeys.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    private static void AppendArgument(Collection<string> arguments, string key, object? value)
    {
        if (value is null)
        {
            return;
        }

        var normalizedKey = NormalizeKey(key);

        if (value is bool boolValue)
        {
            if (boolValue)
            {
                arguments.Add(normalizedKey);
            }

            return;
        }

        arguments.Add(normalizedKey);
        arguments.Add(value.ToString() ?? string.Empty);
    }

    private static string NormalizeKey(string key)
    {
        var trimmed = key.Trim().TrimStart('-').Replace('_', '-');
        return $"--{trimmed}";
    }

    private static string SerializeArguments(IReadOnlyDictionary<string, object?> arguments)
    {
        var snapshot = arguments.ToDictionary(
            pair => pair.Key,
            pair => NormalizeArgumentValue(pair.Value),
            StringComparer.OrdinalIgnoreCase);

        return JsonSerializer.Serialize(snapshot, LogJsonOptions);
    }

    private static object? NormalizeArgumentValue(object? value)
    {
        return value switch
        {
            null => null,
            JsonElement jsonElement => JsonSerializer.Deserialize<object?>(jsonElement.GetRawText(), LogJsonOptions),
            _ => value,
        };
    }

    private static string TruncateForLog(string value)
    {
        const int maxLength = 4000;

        if (value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..maxLength]}...(truncated, total {value.Length} chars)";
    }

    private static string TruncateForDebugLog(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        const int maxLength = 12000;
        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed.Replace(Environment.NewLine, "\\n");
        }

        return $"{trimmed[..maxLength].Replace(Environment.NewLine, "\\n")}...(truncated, total {trimmed.Length} chars)";
    }

    private void WriteSkillDebugLog(string eventName, string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(skillDebugLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var entry = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{eventName}] {message}{Environment.NewLine}";
            lock (SkillDebugLogSync)
            {
                File.AppendAllText(skillDebugLogPath, entry);
            }
        }
        catch
        {
        }
    }

    private static string RenderCommand(ProcessStartInfo startInfo)
    {
        var renderedArguments = startInfo.ArgumentList.Select(QuoteArgumentIfNeeded);
        return string.Join(" ", new[] { QuoteArgumentIfNeeded(startInfo.FileName) }.Concat(renderedArguments));
    }

    private static string QuoteArgumentIfNeeded(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (value.Any(char.IsWhiteSpace) || value.Contains('"'))
        {
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }

        return value;
    }

    private static Dictionary<string, object?> ToArgumentDictionary(IEnumerable<KeyValuePair<string, object?>> arguments)
    {
        var argumentMap = arguments.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        return TryExtractWrappedJsonObject(argumentMap, out var normalizedArguments)
            ? normalizedArguments
            : argumentMap;
    }

    private static bool TryExtractWrappedJsonObject(
        IReadOnlyDictionary<string, object?> arguments,
        out Dictionary<string, object?> normalizedArguments)
    {
        foreach (var key in new[] { "arguments", "json", "payload", "input" })
        {
            if (!TryGetArgument(arguments, key, out var candidate) || candidate is null)
            {
                continue;
            }

            if (TryConvertToJsonObject(candidate, out normalizedArguments))
            {
                return true;
            }
        }

        if (arguments.Count == 1)
        {
            var candidate = arguments.First().Value;
            if (candidate is not null && TryConvertToJsonObject(candidate, out normalizedArguments))
            {
                return true;
            }
        }

        normalizedArguments = null!;
        return false;
    }

    private static bool TryConvertToJsonObject(object value, out Dictionary<string, object?> normalizedArguments)
    {
        switch (value)
        {
            case IReadOnlyDictionary<string, object?> dictionary:
                normalizedArguments = dictionary.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                return true;

            case JsonElement { ValueKind: JsonValueKind.Object } jsonElement:
                normalizedArguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                    jsonElement.GetRawText(),
                    LogJsonOptions) ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                return true;

            case string text when LooksLikeJsonObject(text):
                normalizedArguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                    text,
                    LogJsonOptions) ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                return true;

            default:
                normalizedArguments = null!;
                return false;
        }
    }

    private static bool LooksLikeJsonObject(string text)
    {
        var trimmed = text.Trim();
        return trimmed.StartsWith('{') && trimmed.EndsWith('}');
    }

    private static Dictionary<string, object?> CreateArguments(params (string Key, object? Value)[] pairs)
    {
        var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in pairs)
        {
            arguments[key] = value;
        }

        return arguments;
    }

    private static readonly string[] ReservedArgumentKeys = ["args", "argv", "positionalArgs", "positional", "_"];

    private static readonly JsonSerializerOptions LogJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private static readonly object SkillDebugLogSync = new();

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}