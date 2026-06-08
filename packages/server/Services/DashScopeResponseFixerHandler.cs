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
/// 本处理器同时兼容 Chat Completions 与 Responses API，
/// 将上述非标准格式统一修正为具名参数 JSON 对象字符串。
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

        modified |= TryFixChatCompletionsArguments(root);
        modified |= TryFixResponsesArguments(root);

        if (root["response"] is JsonNode responseNode)
        {
            modified |= TryFixResponsesArguments(responseNode);
        }

        return modified ? root.ToJsonString() : null;
    }

    private static bool TryFixChatCompletionsArguments(JsonNode root)
    {
        var modified = false;
        var choices = root["choices"]?.AsArray();
        if (choices is null)
        {
            return false;
        }

        foreach (var choice in choices)
        {
            var toolCalls = choice?["message"]?["tool_calls"]?.AsArray();
            if (toolCalls is null)
            {
                continue;
            }

            foreach (var toolCall in toolCalls)
            {
                var function = toolCall?["function"];
                if (function is null)
                {
                    continue;
                }

                var functionName = function["name"]?.GetValue<string>();
                var fixedArguments = FunctionCallArgumentNormalization.TryNormalizeArgumentsJsonNode(
                    functionName,
                    function["arguments"]);

                if (fixedArguments is null)
                {
                    continue;
                }

                function["arguments"] = JsonValue.Create(fixedArguments);
                modified = true;
            }
        }

        return modified;
    }

    private static bool TryFixResponsesArguments(JsonNode root)
    {
        var modified = false;
        var output = root["output"]?.AsArray();
        if (output is null)
        {
            return false;
        }

        foreach (var item in output)
        {
            var type = item?["type"]?.GetValue<string>();
            if (!string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var functionName = item?["name"]?.GetValue<string>();
            var fixedArguments = FunctionCallArgumentNormalization.TryNormalizeArgumentsJsonNode(
                functionName,
                item?["arguments"]);

            if (fixedArguments is null)
            {
                continue;
            }

            item!["arguments"] = JsonValue.Create(fixedArguments);
            modified = true;
        }

        return modified;
    }
}
