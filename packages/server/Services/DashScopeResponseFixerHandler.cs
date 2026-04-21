using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SchoolRecommender.Api.Services;

/// <summary>
/// HTTP 消息处理器，在 OpenAI SDK 反序列化响应之前拦截并修正 DashScope 返回的
/// function call arguments 格式错误问题。
///
/// DashScope/Qwen 有时将 tool_call 的 arguments 以 JSON 数组形式返回，例如：
///   "arguments": "[\"学校名\"]"   （数组序列化为字符串）
///   "arguments": ["学校名"]       （直接返回数组节点，非标准）
///
/// 标准 OpenAI 格式应为：
///   "arguments": "{\"keyword\": \"学校名\"}"
///
/// 本处理器将上述非标准格式统一修正为具名参数 JSON 对象字符串。
/// </summary>
public sealed class DashScopeResponseFixerHandler : DelegatingHandler
{
    public DashScopeResponseFixerHandler() : base(new HttpClientHandler()) { }

    public DashScopeResponseFixerHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!IsJsonResponse(response))
        {
            return response;
        }

        try
        {
            var originalBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var fixedBody = TryFixArgumentsInBody(originalBody);

            if (fixedBody is null)
            {
                return response;
            }

            // 重建响应体
            var newContent = new StringContent(fixedBody, Encoding.UTF8, "application/json");

            // 复制原始响应头到新 content
            foreach (var header in response.Content.Headers)
            {
                if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    continue;

                newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            response.Content = newContent;
        }
        catch
        {
            // 解析/修正失败不影响原始响应，让 SDK 原样处理（可能后续报错）
        }

        return response;
    }

    private static bool IsJsonResponse(HttpResponseMessage response)
    {
        return response.IsSuccessStatusCode
            && response.Content.Headers.ContentType?.MediaType is "application/json";
    }

    /// <summary>
    /// 尝试修正响应体中所有 tool_call 的 arguments 字段。
    /// 如果没有任何修改则返回 null。
    /// </summary>
    private static string? TryFixArgumentsInBody(string body)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(body);
        }
        catch
        {
            return null;
        }

        if (root is null) return null;

        var modified = false;

        // 遍历 choices[*].message.tool_calls[*].function.arguments
        var choices = root["choices"]?.AsArray();
        if (choices is null) return null;

        foreach (var choice in choices)
        {
            if (choice is null) continue;

            var message = choice["message"];
            if (message is null) continue;

            var toolCalls = message["tool_calls"]?.AsArray();
            if (toolCalls is null) continue;

            foreach (var toolCall in toolCalls)
            {
                if (toolCall is null) continue;

                var function = toolCall["function"];
                if (function is null) continue;

                var functionName = function["name"]?.GetValue<string>();
                var argumentsNode = function["arguments"];
                if (argumentsNode is null) continue;

                string? fixedArguments = null;

                if (argumentsNode is JsonValue jsonValue)
                {
                    // 正常情况：arguments 是字符串，值可能是数组 JSON 字符串 "[\"keyword\"]"
                    var raw = jsonValue.GetValue<string>();
                    fixedArguments = TryFixArgumentsString(raw, functionName);
                }
                else if (argumentsNode is JsonArray jsonArray)
                {
                    // 非标准情况：arguments 直接是 JSON 数组节点
                    fixedArguments = ArrayToNamedArgsJson(jsonArray, functionName);
                }

                if (fixedArguments is not null)
                {
                    function["arguments"] = JsonValue.Create(fixedArguments);
                    modified = true;
                }
            }
        }

        return modified ? root.ToJsonString() : null;
    }

    /// <summary>
    /// 如果 arguments 字符串解析后为数组或纯字符串值，则转换为具名参数对象字符串。
    /// </summary>
    private static string? TryFixArgumentsString(string? raw, string? functionName)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var trimmed = raw.Trim();

        // 已经是对象格式，无需修复
        if (trimmed.StartsWith('{'))
        {
            return null;
        }

        // 数组格式：解析后转换为具名参数对象
        if (trimmed.StartsWith('['))
        {
            try
            {
                var arrayNode = JsonNode.Parse(trimmed) as JsonArray;
                if (arrayNode is null) return null;

                return ArrayToNamedArgsJson(arrayNode, functionName);
            }
            catch
            {
                return null;
            }
        }

        // 纯字符串值（非 JSON 格式）：作为主参数值包装为具名参数对象
        var primaryParam = GetPrimaryParameterName(functionName);
        return JsonSerializer.Serialize(new Dictionary<string, string> { [primaryParam] = trimmed });
    }

    /// <summary>
    /// 将 JSON 数组转为具名参数对象的 JSON 字符串。
    /// 根据函数名推断主参数名称。
    /// </summary>
    private static string? ArrayToNamedArgsJson(JsonArray array, string? functionName)
    {
        var items = array.ToList();
        if (items.Count == 0) return null;

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var primaryParam = GetPrimaryParameterName(functionName);
        result[primaryParam] = items[0]?.GetValue<object>();

        var secondaryParams = GetSecondaryParameterNames(functionName);
        for (var i = 1; i < items.Count && i - 1 < secondaryParams.Length; i++)
        {
            result[secondaryParams[i - 1]] = items[i]?.GetValue<object>();
        }

        return JsonSerializer.Serialize(result);
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
