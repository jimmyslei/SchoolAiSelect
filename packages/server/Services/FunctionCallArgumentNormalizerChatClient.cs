using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using SchoolRecommender.Api.Options;

namespace SchoolRecommender.Api.Services;

/// <summary>
/// 修复某些 LLM（如 DashScope/Qwen）在 function calling 时将参数以 JSON 数组形式传递的问题。
/// 例如：{"arguments": ["学校名"]} 或根节点直接是 ["学校名"]。
/// 正常格式应为：{"keyword": "学校名"} 或 {"query": "学校名"}。
/// </summary>
public sealed class FunctionCallArgumentNormalizerChatClient : DelegatingChatClient
{
    private readonly ILogger<FunctionCallArgumentNormalizerChatClient>? _logger;
    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private readonly TimeSpan _minRequestInterval;
    private readonly Lock _scheduleLock = new();
    private DateTimeOffset _nextAllowedRequestAt = DateTimeOffset.MinValue;

    public FunctionCallArgumentNormalizerChatClient(
        IChatClient inner,
        SchoolServiceOptions options,
        ILogger<FunctionCallArgumentNormalizerChatClient>? logger = null) : base(inner)
    {
        _logger = logger;
        _maxRetries = options.DashScopeRateLimitMaxRetries;
        _baseDelay = options.DashScopeRateLimitBaseDelay;
        _maxDelay = options.DashScopeRateLimitMaxDelay;
        _minRequestInterval = options.DashScopeMinRequestInterval;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            await WaitForRequestTurnAsync(cancellationToken);

            try
            {
                var response = await base.GetResponseAsync(messages, options, cancellationToken);
                NormalizeResponse(response);
                return response;
            }
            catch (Exception ex) when (ShouldRetry(ex, attempt, out var delay))
            {
                _logger?.LogWarning(
                    ex,
                    "大模型接口触发限流，准备第 {RetryAttempt} 次重试，{DelayMs}ms 后继续。",
                    attempt + 1,
                    (int)delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            await WaitForRequestTurnAsync(cancellationToken);

            var yieldedAny = false;

            await using var enumerator = base.GetStreamingResponseAsync(messages, options, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

            while (true)
            {
                ChatResponseUpdate update;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        yield break;
                    }

                    update = enumerator.Current;
                    yieldedAny = true;
                }

                catch (Exception ex) when (!yieldedAny && ShouldRetry(ex, attempt, out var delay))
                {
                    _logger?.LogWarning(
                        ex,
                        "流式大模型接口触发限流，准备第 {RetryAttempt} 次重试，{DelayMs}ms 后继续。",
                        attempt + 1,
                        (int)delay.TotalMilliseconds);

                    await Task.Delay(delay, cancellationToken);
                    break;
                }

                yield return update;
            }
        }
    }

    private async Task WaitForRequestTurnAsync(CancellationToken cancellationToken)
    {
        if (_minRequestInterval <= TimeSpan.Zero)
        {
            return;
        }

        TimeSpan delay;
        lock (_scheduleLock)
        {
            var now = DateTimeOffset.UtcNow;
            var scheduledAt = _nextAllowedRequestAt > now ? _nextAllowedRequestAt : now;
            _nextAllowedRequestAt = scheduledAt + _minRequestInterval;
            delay = scheduledAt - now;
        }

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }
    }

    private bool ShouldRetry(Exception ex, int attempt, out TimeSpan delay)
    {
        delay = TimeSpan.Zero;

        if (attempt >= _maxRetries || !IsRateLimitException(ex))
        {
            return false;
        }

        delay = ComputeDelay(attempt);
        return true;
    }

    private TimeSpan ComputeDelay(int attempt)
    {
        var factor = Math.Pow(2, attempt);
        var computedDelayMs = Math.Min(_baseDelay.TotalMilliseconds * factor, _maxDelay.TotalMilliseconds);
        var jitter = 0.85 + (Random.Shared.NextDouble() * 0.3);
        return TimeSpan.FromMilliseconds(Math.Max(100, computedDelayMs * jitter));
    }

    private static bool IsRateLimitException(Exception ex)
    {
        if (ex is ClientResultException clientResultException && clientResultException.Status == 429)
        {
            return true;
        }

        if (ex.InnerException is not null)
        {
            return IsRateLimitException(ex.InnerException);
        }

        return ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("rate_limit", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase);
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
