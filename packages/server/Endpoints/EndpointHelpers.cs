using System.Text;
using System.Text.Json;
using SchoolRecommender.Api.Models;

namespace SchoolRecommender.Api.Endpoints;

internal static class EndpointSseHelpers
{
    public static async Task WriteJsonErrorAsync(HttpContext context, int statusCode, string message, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message), cancellationToken);
    }

    public static void ConfigureSseResponse(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream; charset=utf-8";
        context.Response.Headers.CacheControl = "no-cache, no-transform";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.Headers["X-Accel-Buffering"] = "no";
    }

    public static async Task WriteSseEventAsync(
        HttpContext context,
        SemaphoreSlim writeLock,
        JsonSerializerOptions jsonOptions,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(payload, jsonOptions);
            var sb = new StringBuilder();
            sb.Append("event: ").Append(eventName).Append('\n');
            foreach (var line in json.Split('\n'))
            {
                sb.Append("data: ").Append(line).Append('\n');
            }

            sb.Append('\n');
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            await context.Response.Body.WriteAsync(bytes, cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public static async Task WriteSseCommentAsync(
        HttpContext context,
        SemaphoreSlim writeLock,
        string comment,
        CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var payload = Encoding.UTF8.GetBytes($": {comment}\n\n");
            await context.Response.Body.WriteAsync(payload, cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public static Task RunSseHeartbeatAsync(
        HttpContext context,
        SemaphoreSlim writeLock,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(interval);
            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    await WriteSseCommentAsync(context, writeLock, "keepalive", cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // request ended
            }
        }, CancellationToken.None);
    }

    public static async Task StopHeartbeatAsync(CancellationTokenSource heartbeatCts, Task? heartbeatTask)
    {
        if (heartbeatTask is null)
        {
            return;
        }

        try
        {
            await heartbeatCts.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            await heartbeatTask;
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
    }
}

internal static class GeoValidator
{
    public static bool IsValidLatitude(double value) => value is >= -90 and <= 90;

    public static bool IsValidLongitude(double value) => value is >= -180 and <= 180;
}
