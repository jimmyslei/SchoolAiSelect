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
    public async Task<string> SearchWechatArticlesAsync(string query, int? num, string? region, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Error: query 不能为空";
        }

        var regionPrefix = ExtractRegionPrefix(region);
        var effectiveQuery = $"{regionPrefix}{query.Trim()}";

        var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["query"] = effectiveQuery,
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
    public async Task<string> SearchXiaohongshuNotesAsync(string keyword, int? limit, string? region, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return "Error: keyword 不能为空";
        }

        var regionPrefix = ExtractRegionPrefix(region);
        var effectiveKeyword = $"{regionPrefix}{keyword.Trim()}";

        var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["keyword"] = effectiveKeyword,
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

    /// <summary>
    /// 学校信息专用联网搜索。基于 skills/web-search/general-web-search/web_search.js，
    /// 但会针对学校场景做查询增强（清洗校名、追加教育领域关键词、强制精确匹配）
    /// 与结果相关性过滤（标题/摘要必须命中校名特征 token），避免 Bing 把超长校名
    /// 降级匹配成"重庆""幼儿园"等通用词、返回旅游/无关内容。
    /// </summary>
    public async Task<string> SearchSchoolInfoAsync(string schoolName, string? aspect, string? region, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(schoolName))
        {
            return "Error: school_name 不能为空";
        }

        var cleanedName = CleanSchoolName(schoolName);
        if (string.IsNullOrWhiteSpace(cleanedName))
        {
            return "Error: 清洗后的 school_name 为空";
        }

        var distinctiveToken = ExtractDistinctiveToken(cleanedName);
        var regionPrefix = ExtractRegionPrefix(region);

        // 将 aspect 转换为具体搜索关键词，追加到查询中，确保每次按不同维度返回差异化结果
        var aspectKeywords = NormalizeAspectKeywords(aspect);
        var query = $"{regionPrefix}{cleanedName} {aspectKeywords}".Trim();

        var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["query"] = query,
            ["num"] = 15,
        };

        var scriptName = Path.Combine("scripts", "search_web.js");
        var scriptFullPath = Path.Combine(options.SkillRootPath, "general-web-search", scriptName);

        logger.LogInformation(
            "Explicit tool invoked. Skill: search_school_info, SchoolName: {SchoolName}, Aspect: {Aspect}, Query: {Query}",
            schoolName, aspect, query);
        WriteSkillDebugLog(
            "EXPLICIT_TOOL_INVOKED",
            $"Tool=search_school_info; Script={scriptName}; SchoolName={schoolName}; Aspect={aspect}; Query={query}; DistinctiveToken={distinctiveToken}");

        var rawResult = await ExecuteScriptAsync(
            "general-web-search",
            scriptName.Replace('\\', '/'),
            scriptFullPath,
            arguments,
            cancellationToken).ConfigureAwait(false);

        var rawText = rawResult?.ToString();
        if (string.IsNullOrWhiteSpace(rawText) || rawText.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
        {
            return rawText ?? "(no output)";
        }

        // 相关性过滤：只保留 title/snippet 命中校名特征的条目
        var filtered = FilterRelevantSchoolResults(rawText, cleanedName, distinctiveToken, schoolName);

        WriteSkillDebugLog(
            "SCHOOL_SEARCH_FILTERED",
            $"SchoolName={schoolName}; DistinctiveToken={distinctiveToken}; Kept={filtered.keptCount}/{filtered.totalCount}");

        return filtered.json;
    }

    private static string CleanSchoolName(string name)
    {
        // 去掉中英文括号及其内容（分校标识/地址别名常导致 Bing 匹配混乱）
        var cleaned = System.Text.RegularExpressions.Regex.Replace(name, @"[（(\[][^）)\]]*[）)\]]", string.Empty);
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();
        return cleaned;
    }

    /// <summary>
    /// 从地址中提取"市+区/县"前缀，用于搜索消歧。
    /// 例："重庆市巴南区鱼轻路10号" → "重庆市巴南区"
    /// </summary>
    private static string ExtractRegionPrefix(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return string.Empty;

        var match = System.Text.RegularExpressions.Regex.Match(
            address,
            @"^(.{2,5}(?:市|州|地区)).{2,6}(?:区|县|市|镇)");
        if (match.Success)
            return match.Value;

        var cityMatch = System.Text.RegularExpressions.Regex.Match(
            address,
            @"^.{2,5}(?:市|州|地区)");
        if (cityMatch.Success)
            return cityMatch.Value;

        return string.Empty;
    }

    /// <summary>
    /// 提取最具区分度的 token，用于相关性过滤：
    /// 优先取"学校类型词"（幼儿园/小学/中学/学校/大学）前最多 6 个汉字。
    /// </summary>
    private static string ExtractDistinctiveToken(string cleanedName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            cleanedName,
            @"([\u4e00-\u9fa5A-Za-z0-9]{2,8})(实验幼儿园|实验小学|实验中学|实验学校|双语学校|国际学校|附属中学|附属小学|附属幼儿园|幼儿园|幼稚园|小学|初中|中学|高中|学校|学院|大学)");

        if (match.Success)
        {
            return match.Groups[1].Value + match.Groups[2].Value;
        }

        // 退化策略：去掉行政前缀
        var stripped = System.Text.RegularExpressions.Regex.Replace(
            cleanedName,
            @"^(.{2,4}省)?(.{2,5}市)?(.{2,5}区|.{2,5}县|.{2,5}州)?",
            string.Empty);
        return string.IsNullOrWhiteSpace(stripped) ? cleanedName : stripped;
    }

    private static string NormalizeAspectKeywords(string? aspect)
    {
        if (string.IsNullOrWhiteSpace(aspect))
        {
            return "评价 师资 家长 招生";
        }

        return aspect.Trim() switch
        {
            "师资" or "teaching" => "师资 教师 教学质量",
            "评价" or "口碑" or "reputation" => "家长 评价 口碑 怎么样",
            "招生" or "enrollment" => "招生 学区 划片 收费",
            "活动" or "校园活动" or "activity" => "活动 比赛 获奖 校园",
            "新闻" or "news" => "新闻 公告 官网",
            _ => aspect.Trim() + " 评价 家长",
        };
    }

    private static (string json, int keptCount, int totalCount) FilterRelevantSchoolResults(
        string rawJson,
        string cleanedName,
        string distinctiveToken,
        string originalName)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);

            // 兼容两种返回结构：
            //  - web_search.py：直接是 [{title,url,snippet}, ...]
            //  - sogou-search：{ query, total, results: [{title,url,summary,source}, ...] }
            JsonElement itemsArray;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                itemsArray = doc.RootElement;
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("results", out var resultsProp)
                && resultsProp.ValueKind == JsonValueKind.Array)
            {
                itemsArray = resultsProp;
            }
            else
            {
                return (rawJson, 0, 0);
            }

            var matchTokens = new List<string>();
            if (!string.IsNullOrWhiteSpace(distinctiveToken)) matchTokens.Add(distinctiveToken);
            if (!string.IsNullOrWhiteSpace(cleanedName) && cleanedName != distinctiveToken) matchTokens.Add(cleanedName);

            // 再加上 distinctive 去掉学校类型后的"核心识别词"（如"小太阳幼儿园" → "小太阳"）
            var coreToken = System.Text.RegularExpressions.Regex.Replace(
                distinctiveToken,
                @"(实验幼儿园|实验小学|实验中学|实验学校|双语学校|国际学校|附属中学|附属小学|附属幼儿园|幼儿园|幼稚园|小学|初中|中学|高中|学校|学院|大学)$",
                string.Empty);
            if (!string.IsNullOrWhiteSpace(coreToken) && coreToken.Length >= 2 && !matchTokens.Contains(coreToken))
            {
                matchTokens.Add(coreToken);
            }

            var kept = new List<object>();
            var total = 0;
            foreach (var item in itemsArray.EnumerateArray())
            {
                total++;
                var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                // 兼容 web_search 的 snippet 与 sogou-search 的 summary
                var snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() ?? string.Empty
                    : item.TryGetProperty("summary", out var sm) ? sm.GetString() ?? string.Empty
                    : string.Empty;
                var url = item.TryGetProperty("url", out var u) ? u.GetString() ?? string.Empty : string.Empty;
                var haystack = title + " " + snippet;

                var hit = matchTokens.Any(tok => haystack.Contains(tok, StringComparison.OrdinalIgnoreCase));
                if (!hit) continue;

                kept.Add(new
                {
                    title,
                    url,
                    snippet,
                });
            }

            // 如果一个都没命中，返回结构化的"无相关结果"提示，避免 LLM 被无关内容污染
            if (kept.Count == 0)
            {
                var hintJson = JsonSerializer.Serialize(new
                {
                    note = $"未检索到与 \"{originalName}\" 相关的公开网页。原始查询返回 {total} 条均与该校无关，已全部过滤。",
                    matched_tokens = matchTokens,
                    results = Array.Empty<object>(),
                }, FilteredResultJsonOptions);
                return (hintJson, 0, total);
            }

            var keptJson = JsonSerializer.Serialize(new
            {
                matched_tokens = matchTokens,
                results = kept,
            }, FilteredResultJsonOptions);
            return (keptJson, kept.Count, total);
        }
        catch (JsonException)
        {
            // 解析失败时直接回传原始文本，让上游决定如何处理
            return (rawJson, 0, 0);
        }
    }

    private static readonly JsonSerializerOptions FilteredResultJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

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

        if (positionalArguments.Count == 0)
        {
            var compatibilityArgs = GetCompatibilityPositionalArguments(scriptStem, values).ToList();
            if (compatibilityArgs.Count > 0)
            {
                positionalArguments.AddRange(compatibilityArgs);
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

        // 已有显式位置参数时，跳过剩余 key-value 参数
        // 注意：兼容性模式（usedCompatibility）不再阻止命名参数传递，
        // 以便 --num / --limit 等命名标志仍能传给脚本（search_web.js / search_wechat.js 均支持 --num）
        if (GetExplicitPositionalArguments(values).Any())
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
            "search_web" => GetPositionalValues(arguments, "query"),
            "search_sogou" => GetPositionalValues(arguments, "query", "keyword", "schoolName"),
            "search" => GetPositionalValues(arguments, "keyword", "query", "schoolName"),
            "web_search" => GetPositionalValues(arguments, "query"),
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
            "search_web" => key.Equals("query", StringComparison.OrdinalIgnoreCase),
            "search_sogou" => key.Equals("query", StringComparison.OrdinalIgnoreCase)
                || key.Equals("keyword", StringComparison.OrdinalIgnoreCase)
                || key.Equals("schoolName", StringComparison.OrdinalIgnoreCase),
            "search" => key.Equals("keyword", StringComparison.OrdinalIgnoreCase)
                || key.Equals("query", StringComparison.OrdinalIgnoreCase)
                || key.Equals("schoolName", StringComparison.OrdinalIgnoreCase),
            "web_search" => key.Equals("query", StringComparison.OrdinalIgnoreCase),
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