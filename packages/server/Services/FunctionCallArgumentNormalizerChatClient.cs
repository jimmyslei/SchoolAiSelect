using System.Text.Json;
using Microsoft.Extensions.AI;

namespace SchoolRecommender.Api.Services;

/// <summary>
/// 修复某些 LLM（如 DashScope/Qwen）在 function calling 时将参数以 JSON 数组形式传递的问题。
/// 例如：{"arguments": ["学校名"]} 或根节点直接是 ["学校名"]。
/// 正常格式应为：{"keyword": "学校名"} 或 {"query": "学校名"}。
/// </summary>
public sealed class FunctionCallArgumentNormalizerChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        NormalizeResponse(response);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
        }
    }

    private static void NormalizeResponse(ChatResponse response)
    {
        foreach (var message in response.Messages)
        {
            for (var i = 0; i < message.Contents.Count; i++)
            {
                if (message.Contents[i] is not FunctionCallContent fcc) continue;

                var normalizedArgs = TryNormalizeArguments(fcc);
                if (normalizedArgs is null) continue;

                message.Contents[i] = new FunctionCallContent(fcc.CallId, fcc.Name, normalizedArgs)
                {
                    RawRepresentation = fcc.RawRepresentation,
                };
            }
        }
    }

    private static Dictionary<string, object?>? TryNormalizeArguments(FunctionCallContent fcc)
    {
        // 情形 1：LLM 发送 {"arguments": ["值"]}
        // 某些模型把 "arguments" 参数名字面化，把实参作为数组放在该键下
        if (fcc.Arguments is not null
            && fcc.Arguments.TryGetValue("arguments", out var argsValue)
            && argsValue is JsonElement { ValueKind: JsonValueKind.Array } argsArrayElement)
        {
            return ArrayToNamedArgs(fcc.Name, argsArrayElement);
        }

        // 情形 2：LLM 发送根节点数组 ["值"] 或纯字符串值
        // 此时 FunctionCallContent.Arguments 可能为 null 或空
        // 尝试通过反射从 RawRepresentation（OpenAI.Chat.ChatToolCall）提取原始 JSON
        if (fcc.RawRepresentation is not null)
        {
            try
            {
                var rawJson = ExtractRawArgumentsJson(fcc.RawRepresentation);
                if (!string.IsNullOrWhiteSpace(rawJson))
                {
                    var trimmed = rawJson.Trim();

                    // 根节点是数组
                    if (trimmed.StartsWith('['))
                    {
                        using var doc = JsonDocument.Parse(trimmed);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            return ArrayToNamedArgs(fcc.Name, doc.RootElement);
                        }
                    }

                    // 根节点是纯字符串值（非 JSON 对象/数组），且当前 arguments 为空
                    if (!trimmed.StartsWith('{') && !trimmed.StartsWith('[')
                        && (fcc.Arguments is null || fcc.Arguments.Count == 0))
                    {
                        var primaryParam = GetPrimaryParameterName(fcc.Name);
                        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            [primaryParam] = trimmed,
                        };
                    }
                }
            }
            catch
            {
                // 解析失败则跳过
            }
        }

        return null;
    }

    /// <summary>
    /// 通过反射从 RawRepresentation 提取函数参数的原始 JSON 字符串。
    /// 支持 OpenAI.Chat.ChatToolCall（属性 FunctionArguments 类型为 BinaryData）。
    /// </summary>
    private static string? ExtractRawArgumentsJson(object rawRepresentation)
    {
        var type = rawRepresentation.GetType();

        // 尝试 FunctionArguments 属性（OpenAI.Chat.ChatToolCall）
        var funcArgsProperty = type.GetProperty("FunctionArguments");
        if (funcArgsProperty is not null)
        {
            var value = funcArgsProperty.GetValue(rawRepresentation);
            return value?.ToString();
        }

        // 尝试 Function.Arguments 路径
        var functionProperty = type.GetProperty("Function");
        if (functionProperty is not null)
        {
            var function = functionProperty.GetValue(rawRepresentation);
            if (function is not null)
            {
                var argsProperty = function.GetType().GetProperty("Arguments");
                if (argsProperty is not null)
                {
                    return argsProperty.GetValue(function)?.ToString();
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 将 JSON 数组转换为具名参数字典，依据函数名推断主参数名称。
    /// </summary>
    private static Dictionary<string, object?>? ArrayToNamedArgs(string? functionName, JsonElement arrayElement)
    {
        var items = arrayElement.EnumerateArray().ToList();
        if (items.Count == 0) return null;

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // 根据函数名推断主参数名
        var primaryParam = GetPrimaryParameterName(functionName);
        result[primaryParam] = items[0].Clone();

        // 如果有额外参数，映射到次要参数（如 xsec_token 等）
        var secondaryParams = GetSecondaryParameterNames(functionName);
        for (var i = 1; i < items.Count && i - 1 < secondaryParams.Length; i++)
        {
            result[secondaryParams[i - 1]] = items[i].Clone();
        }

        return result;
    }

    private static string GetPrimaryParameterName(string? functionName) =>
        functionName switch
        {
            var n when n is not null && n.Contains("wechat", StringComparison.OrdinalIgnoreCase) => "query",
            var n when n is not null && n.Contains("feed", StringComparison.OrdinalIgnoreCase) => "feed_id",
            var n when n is not null && n.Contains("user", StringComparison.OrdinalIgnoreCase) => "user_id",
            _ => "keyword",
        };

    private static string[] GetSecondaryParameterNames(string? functionName) =>
        functionName switch
        {
            var n when n is not null && n.Contains("feed", StringComparison.OrdinalIgnoreCase) => ["xsec_token"],
            var n when n is not null && n.Contains("user", StringComparison.OrdinalIgnoreCase) => ["xsec_token"],
            _ => [],
        };
}
