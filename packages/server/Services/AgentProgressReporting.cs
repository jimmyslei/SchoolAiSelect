using System.Text.Json;
using SchoolRecommender.Api.Models;

namespace SchoolRecommender.Api.Services;

public interface IAgentProgressReporter
{
    ValueTask ReportAsync(AgentExecutionStep step, CancellationToken cancellationToken = default);
}

public sealed class DelegateAgentProgressReporter(
    Func<AgentExecutionStep, CancellationToken, ValueTask> callback) : IAgentProgressReporter
{
    public ValueTask ReportAsync(AgentExecutionStep step, CancellationToken cancellationToken = default)
        => callback(step, cancellationToken);
}

public sealed record AgentProgressScope(
    string Scope,
    string? EntityId,
    string? EntityName,
    IAgentProgressReporter? Reporter);

public sealed class AgentProgressContext
{
    private readonly AsyncLocal<AgentProgressScope?> _current = new();

    public AgentProgressScope? Current => _current.Value;

    public IDisposable Push(AgentProgressScope scope)
    {
        var previous = _current.Value;
        _current.Value = scope;
        return new RestoreDisposable(() => _current.Value = previous);
    }

    private sealed class RestoreDisposable(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _onDispose, null)?.Invoke();
        }
    }
}

public static class AgentProgressFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    public static AgentExecutionStep CreateStep(
        AgentProgressScope? scope,
        string status,
        string message,
        string? toolName = null,
        IReadOnlyDictionary<string, object?>? toolArguments = null,
        string? toolResult = null)
        => new(
            Id: Guid.NewGuid().ToString("N"),
            Scope: scope?.Scope ?? "agent-analysis",
            Status: status,
            Message: message,
            Timestamp: DateTimeOffset.UtcNow,
            EntityId: scope?.EntityId,
            EntityName: scope?.EntityName,
            ToolName: toolName,
            ToolArguments: toolArguments,
            ToolResult: toolResult);

    public static string? Preview(object? value, int maxLength = 500)
    {
        if (value is null)
        {
            return null;
        }

        string text;
        if (value is string stringValue)
        {
            text = stringValue;
        }
        else
        {
            try
            {
                text = JsonSerializer.Serialize(value, JsonOptions);
            }
            catch
            {
                text = value.ToString() ?? string.Empty;
            }
        }

        text = text.Replace("\r\n", "\n").Trim();
        if (text.Length <= maxLength)
        {
            return text;
        }

        return $"{text[..maxLength]}…";
    }
}