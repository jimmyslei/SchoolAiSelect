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
            var hasFunctionCall = false;
            for (var i = 0; i < message.Contents.Count; i++)
            {
                if (message.Contents[i] is not FunctionCallContent fcc) continue;

                hasFunctionCall = true;

                var normalizedArgs = TryNormalizeArguments(fcc);
                if (normalizedArgs is null) continue;

                message.Contents[i] = new FunctionCallContent(fcc.CallId, fcc.Name, normalizedArgs)
                {
                    RawRepresentation = fcc.RawRepresentation,
                };
            }

            if (!hasFunctionCall
                && TryGetText(message, response) is { } text
                && FunctionCallArgumentNormalization.TryParseMimoToolCall(text) is { } mimoToolCall)
            {
                message.Contents.Clear();
                message.Contents.Add(new FunctionCallContent(
                    $"mimo_{Guid.NewGuid():N}",
                    mimoToolCall.FunctionName,
                    mimoToolCall.Arguments));
            }
        }
    }

    private static string? TryGetText(Microsoft.Extensions.AI.ChatMessage message, ChatResponse response)
    {
        var text = string.Concat(message.Contents.OfType<TextContent>().Select(content => content.Text));
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return string.IsNullOrWhiteSpace(response.Text) ? null : response.Text;
    }

    private static Dictionary<string, object?>? TryNormalizeArguments(FunctionCallContent fcc)
    {
        try
        {
            return FunctionCallArgumentNormalization.TryNormalizeArguments(
                fcc.Name,
                fcc.Arguments,
                fcc.RawRepresentation);
        }
        catch
        {
            return null;
        }
    }
}
