using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Services;
using static SchoolRecommender.Api.Endpoints.EndpointSseHelpers;

namespace SchoolRecommender.Api.Endpoints;

internal static class HighSchoolEndpoints
{
    public static void MapHighSchoolEndpoints(this WebApplication app)
    {
        var highSchoolGroup = app.MapGroup("/api/highschools")
            .WithTags("HighSchools")
            .RequireRateLimiting("api");

        highSchoolGroup.MapPost("/recommend",
            async (
                HttpContext httpContext,
                [FromBody] HighSchoolRecommendRequest request,
                HighSchoolRecommendAgent agent,
                HighSchoolRankingService rankingService,
                AuthService authService,
                HistoryService historyService,
                CancellationToken cancellationToken) =>
            {
                var user = await authService.GetUserFromRequestAsync(httpContext);
                if (user is null)
                {
                    await WriteJsonErrorAsync(httpContext, StatusCodes.Status401Unauthorized, "请先登录后再开始分析", cancellationToken);
                    return;
                }

                if (request.StudentScore is < 100 or > 900)
                {
                    await WriteJsonErrorAsync(httpContext, StatusCodes.Status400BadRequest, "请输入有效的考试分数 (100-900)", cancellationToken);
                    return;
                }

                if (string.IsNullOrWhiteSpace(request.Province))
                {
                    await WriteJsonErrorAsync(httpContext, StatusCodes.Status400BadRequest, "请选择所在省份", cancellationToken);
                    return;
                }

                ConfigureSseResponse(httpContext);

                var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
                var writeLock = new SemaphoreSlim(1, 1);
                using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task? heartbeatTask = null;

                Task WriteEventAsync(string eventName, object payload)
                    => WriteSseEventAsync(httpContext, writeLock, jsonOptions, eventName, payload, cancellationToken);

                try
                {
                    await WriteEventAsync("meta", new
                    {
                        studentScore = request.StudentScore,
                        province = request.Province,
                    });
                    heartbeatTask = RunSseHeartbeatAsync(httpContext, writeLock, TimeSpan.FromSeconds(10), heartbeatCts.Token);

                    var progressReporter = new DelegateAgentProgressReporter(
                        (step, ct) => new ValueTask(WriteEventAsync("agent_step", new { step })));

                    var recommendations = await agent.RecommendAsync(
                        request.StudentScore,
                        request.Province,
                        progressReporter,
                        cancellationToken,
                        request.City);

                    if (recommendations.Count == 0)
                    {
                        await WriteEventAsync("done", new
                        {
                            recommendations = Array.Empty<HighSchoolRecommendation>(),
                            message = $"暂未找到 {request.Province} 高中的有效录取数据，请稍后重试",
                        });
                        return;
                    }

                    var normalized = rankingService.Normalize(recommendations, request.StudentScore);
                    var donePayloadHs = new
                    {
                        studentScore = request.StudentScore,
                        province = request.Province,
                        recommendations = normalized,
                    };
                    await WriteEventAsync("done", donePayloadHs);

                    try
                    {
                        var title = $"{request.Province}·高中择校·{request.StudentScore}分";
                        var summary = $"共推荐 {normalized.Count} 所高中";
                        var requestJson = JsonSerializer.Serialize(request, jsonOptions);
                        var resultJson = JsonSerializer.Serialize(donePayloadHs, jsonOptions);
                        await historyService.SaveAsync(user.Id, "highschool", title, requestJson, resultJson, summary, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        app.Logger.LogWarning(ex, "保存高中推荐历史失败, userId={UserId}", user.Id);
                    }
                }
                catch (OperationCanceledException)
                {
                    // client disconnected
                }
                catch (Exception exception)
                {
                    try
                    {
                        await WriteEventAsync("error", new { message = exception.Message });
                    }
                    catch { }
                }
                finally
                {
                    await StopHeartbeatAsync(heartbeatCts, heartbeatTask);
                    writeLock.Dispose();
                }
            })
            .WithName("RecommendHighSchools")
            .WithSummary("根据学生分数和所在省份，按冲/稳/保三档推荐适合报考的高中");
    }
}
