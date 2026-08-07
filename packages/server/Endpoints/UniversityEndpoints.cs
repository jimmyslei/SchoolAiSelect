using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Services;
using static SchoolRecommender.Api.Endpoints.EndpointSseHelpers;

namespace SchoolRecommender.Api.Endpoints;

internal static class UniversityEndpoints
{
    public static void MapUniversityEndpoints(this WebApplication app)
    {
        var universityGroup = app.MapGroup("/api/universities")
            .WithTags("Universities")
            .RequireRateLimiting("api");

        universityGroup.MapPost("/search",
            async (
                HttpContext httpContext,
                [FromBody] UniversitySearchRequest request,
                UniversityAnalysisOrchestrator orchestrator,
                UniversityRankingService rankingService,
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

                if (request.StudentScore is < 0 or > 750)
                {
                    await WriteJsonErrorAsync(httpContext, StatusCodes.Status400BadRequest, "请输入有效的高考分数 (0-750)", cancellationToken);
                    return;
                }

                var isRecommendationMode = string.Equals(request.SearchMode, "recommend", StringComparison.OrdinalIgnoreCase);

                if (!isRecommendationMode && request.UniversityNames is not { Count: > 0 })
                {
                    await WriteJsonErrorAsync(httpContext, StatusCodes.Status400BadRequest, "请至少提供一所意向大学", cancellationToken);
                    return;
                }

                if (request.UniversityNames is { Count: > 10 })
                {
                    await WriteJsonErrorAsync(httpContext, StatusCodes.Status400BadRequest, "最多同时分析 10 所大学", cancellationToken);
                    return;
                }

                if (isRecommendationMode
                    && request.PreferredMajors is not { Count: > 0 }
                    && request.PreferredDirections is not { Count: > 0 }
                    && string.IsNullOrWhiteSpace(request.PersonalPreferences))
                {
                    await WriteJsonErrorAsync(httpContext, StatusCodes.Status400BadRequest, "推荐模式下请至少填写喜欢的方向、意向专业或个人偏好", cancellationToken);
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
                        searchMode = isRecommendationMode ? "recommend" : "manual",
                        studentScore = request.StudentScore,
                        universityNames = request.UniversityNames,
                        preferredMajors = request.PreferredMajors,
                        preferredDirections = request.PreferredDirections,
                        personalPreferences = request.PersonalPreferences,
                        totalCount = isRecommendationMode ? 0 : request.UniversityNames?.Count ?? 0,
                    });
                    heartbeatTask = RunSseHeartbeatAsync(httpContext, writeLock, TimeSpan.FromSeconds(10), heartbeatCts.Token);

                    var analyses = new List<UniversityAnalysis>();
                    var progressReporter = new DelegateAgentProgressReporter(
                        (step, ct) => new ValueTask(WriteEventAsync("agent_step", new { step })));

                    if (isRecommendationMode)
                    {
                        await foreach (var (universityName, analysis) in orchestrator.RecommendStreamAsync(
                            request.StudentScore,
                            request.PreferredMajors,
                            request.PreferredDirections,
                            request.PersonalPreferences,
                            progressReporter,
                            cancellationToken))
                        {
                            analyses.Add(analysis);
                            await WriteEventAsync("analysis", new
                            {
                                universityName,
                                analysis,
                            });
                        }
                    }
                    else
                    {
                        await foreach (var (universityName, analysis) in orchestrator.AnalyzeStreamAsync(
                            request.UniversityNames!, request.StudentScore, request.PreferredMajors, progressReporter, cancellationToken))
                        {
                            analyses.Add(analysis);
                            await WriteEventAsync("analysis", new
                            {
                                universityName,
                                analysis,
                            });
                        }
                    }

                    var rankings = rankingService.RankUniversities(analyses, request.StudentScore);
                    var donePayloadUni = new
                    {
                        searchMode = isRecommendationMode ? "recommend" : "manual",
                        studentScore = request.StudentScore,
                        universityNames = request.UniversityNames,
                        preferredDirections = request.PreferredDirections,
                        personalPreferences = request.PersonalPreferences,
                        rankings,
                        analyzedCount = rankings.Count,
                        totalCount = isRecommendationMode ? rankings.Count : request.UniversityNames?.Count ?? rankings.Count,
                    };
                    await WriteEventAsync("done", donePayloadUni);

                    try
                    {
                        var uniNames = isRecommendationMode
                            ? string.Join("、", rankings.Select(r => r.Analysis.Info.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(3))
                            : string.Join("、", request.UniversityNames!.Take(3));
                        var title = isRecommendationMode
                            ? $"{request.StudentScore}分·智能推荐{(string.IsNullOrWhiteSpace(uniNames) ? string.Empty : $"·{uniNames}")}{(rankings.Count > 3 ? "等" : "")}" 
                            : $"{request.StudentScore}分·{uniNames}{(request.UniversityNames!.Count > 3 ? "等" : "")}";
                        var summary = isRecommendationMode
                            ? $"按偏好智能推荐 {rankings.Count} 所大学"
                            : $"共分析 {rankings.Count} 所大学";
                        var requestJson = JsonSerializer.Serialize(request, jsonOptions);
                        var resultJson = JsonSerializer.Serialize(donePayloadUni, jsonOptions);
                        await historyService.SaveAsync(user.Id, "university", title, requestJson, resultJson, summary, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        app.Logger.LogWarning(ex, "保存大学搜索历史失败, userId={UserId}", user.Id);
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
            .WithName("SearchUniversities")
            .WithSummary("根据高考成绩和意向大学，或根据分数与偏好直接推荐大学与专业");
    }
}
