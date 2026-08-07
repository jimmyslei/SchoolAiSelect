using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Services;
using static SchoolRecommender.Api.Endpoints.EndpointSseHelpers;

namespace SchoolRecommender.Api.Endpoints;

internal static class SchoolEndpoints
{
    public static void MapSchoolEndpoints(this WebApplication app)
    {
        var schoolGroup = app.MapGroup("/api/schools")
            .WithTags("Schools")
            .RequireRateLimiting("api");

        schoolGroup.MapPost("/search",
            async (
                HttpContext httpContext,
                [FromBody] SearchRequest request,
                AmapService amapService,
                SchoolAnalysisOrchestrator orchestrator,
                SchoolRankingService rankingService,
                AnalyticsService analyticsService,
                HistoryService historyService,
                AuthService authService,
                CancellationToken cancellationToken) =>
            {
                var user = await authService.GetUserFromRequestAsync(httpContext);
                if (user is null)
                {
                    await WriteJsonErrorAsync(httpContext, StatusCodes.Status401Unauthorized, "请先登录后再开始分析", cancellationToken);
                    return;
                }

                if (!GeoValidator.IsValidLatitude(request.Latitude) || !GeoValidator.IsValidLongitude(request.Longitude))
                {
                    await WriteJsonErrorAsync(httpContext, StatusCodes.Status400BadRequest, "经纬度范围无效", cancellationToken);
                    return;
                }

                if (!SchoolTypes.IsValid(request.SchoolType))
                {
                    await WriteJsonErrorAsync(httpContext, StatusCodes.Status400BadRequest, $"无效的学校类型: {request.SchoolType}", cancellationToken);
                    return;
                }

                var radius = request.Radius is > 0 and <= 20000 ? request.Radius.Value : 5000;

                ConfigureSseResponse(httpContext);

                var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
                var writeLock = new SemaphoreSlim(1, 1);
                using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task? heartbeatTask = null;

                Task WriteEventAsync(string eventName, object payload)
                    => WriteSseEventAsync(httpContext, writeLock, jsonOptions, eventName, payload, cancellationToken);

                try
                {
                    var searchSessionId = Guid.NewGuid().ToString("N");
                    var clientIp = httpContext.Connection.RemoteIpAddress?.ToString();
                    var userAgent = httpContext.Request.Headers.UserAgent.ToString();
                    var location = await amapService.ReverseGeocodeAsync(request.Longitude, request.Latitude, cancellationToken);
                    var pois = await amapService.SearchNearbySchoolsAsync(request.Longitude, request.Latitude, request.SchoolType, radius, cancellationToken);
                    var schools = pois.Select(poi => SchoolMapper.FromPoi(poi, request.SchoolType)).ToArray();

                    await WriteEventAsync("meta", new
                    {
                        location = new SearchLocation(location.Address, request.Latitude, request.Longitude),
                        schools,
                        searchRadius = radius,
                        totalFound = schools.Length,
                    });
                    heartbeatTask = RunSseHeartbeatAsync(httpContext, writeLock, TimeSpan.FromSeconds(10), heartbeatCts.Token);

                    if (schools.Length == 0)
                    {
                        await WriteEventAsync("done", new
                        {
                            rankings = Array.Empty<SchoolRanking>(),
                            message = "附近未找到相关学校，请尝试扩大搜索范围",
                        });
                        return;
                    }

                    var schoolMap = schools.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
                    var analyses = new Dictionary<string, SchoolAnalysis>(StringComparer.OrdinalIgnoreCase);
                    var progressReporter = new DelegateAgentProgressReporter(
                        (step, ct) => new ValueTask(WriteEventAsync("agent_step", new { step })));

                    await foreach (var (schoolId, analysis) in orchestrator.AnalyzeStreamAsync(schools, request.StudentScore, progressReporter, cancellationToken))
                    {
                        analyses[schoolId] = analysis;
                        if (!schoolMap.TryGetValue(schoolId, out var school))
                        {
                            continue;
                        }

                        var partialScore = rankingService.CalculateOverallScore(school, analysis);
                        var admissionMatch = request.StudentScore.HasValue && SchoolTypes.IsMiddleSchool(request.SchoolType)
                            ? rankingService.CalculateAdmissionMatchScore(analysis, request.StudentScore.Value)
                            : (int?)null;
                        await WriteEventAsync("analysis", new
                        {
                            schoolId,
                            analysis,
                            overallScore = partialScore,
                            admissionMatchScore = admissionMatch,
                        });
                    }

                    var rankings = rankingService.RankSchools(schools, analyses, null, request.StudentScore);
                    await analyticsService.RecordSchoolSearchAsync(
                        searchSessionId,
                        request,
                        location.Address,
                        rankings,
                        clientIp,
                        userAgent,
                        cancellationToken);

                    var donePayload = new
                    {
                        schoolType = request.SchoolType,
                        locationAddress = location.Address,
                        rankings,
                        analyzedCount = rankings.Count,
                        totalCount = rankings.Count,
                    };
                    await WriteEventAsync("done", donePayload);

                    try
                    {
                        var schoolTypeLabel = SchoolTypes.DisplayName(request.SchoolType);
                        var title = $"{location.Address}·{schoolTypeLabel}";
                        var summary = $"共分析 {rankings.Count} 所学校";
                        var requestJson = JsonSerializer.Serialize(request, jsonOptions);
                        var resultJson = JsonSerializer.Serialize(donePayload, jsonOptions);
                        await historyService.SaveAsync(user.Id, request.SchoolType, title, requestJson, resultJson, summary, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        app.Logger.LogWarning(ex, "保存学校搜索历史失败, userId={UserId}", user.Id);
                    }
                }
                catch (OperationCanceledException)
                {
                    // client disconnected, nothing to do
                }
                catch (Exception exception)
                {
                    try
                    {
                        await WriteEventAsync("error", new { message = exception.Message });
                    }
                    catch
                    {
                        // best-effort error notification
                    }
                }
                finally
                {
                    await StopHeartbeatAsync(heartbeatCts, heartbeatTask);
                    writeLock.Dispose();
                }
            })
            .WithName("SearchSchools")
            .WithSummary("搜索附近学校并以 SSE 流式返回分析与排名");

        schoolGroup.MapGet("/{id}",
            Results<StatusCodeHttpResult, JsonHttpResult<ApiResponse<object>>> ([FromRoute] string id) =>
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return TypedResults.Json(ApiResponse<object>.Fail("学校编号不能为空"), statusCode: StatusCodes.Status400BadRequest);
                }

                return TypedResults.StatusCode(StatusCodes.Status501NotImplemented);
            })
            .WithName("GetSchoolDetail")
            .WithSummary("获取单个学校详情")
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status501NotImplemented);

        schoolGroup.MapGet("/nearby",
            async (
                double latitude,
                double longitude,
                string type,
                int? radius,
                AmapService amapService,
                CancellationToken cancellationToken) =>
            {
                if (!GeoValidator.IsValidLatitude(latitude) || !GeoValidator.IsValidLongitude(longitude))
                {
                    return Results.Json(ApiResponse<object>.Fail("经纬度范围无效"), statusCode: StatusCodes.Status400BadRequest);
                }

                var validTypes = new[] { "subway", "hospital", "supermarket", "park", "library", "bookstore" };
                if (!validTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
                {
                    return Results.Json(ApiResponse<object>.Fail($"不支持的设施类型: {type}，支持: {string.Join(", ", validTypes)}"), statusCode: StatusCodes.Status400BadRequest);
                }

                var searchRadius = radius is > 0 and <= 10000 ? radius.Value : 2000;

                try
                {
                    var facilities = await amapService.SearchNearbyFacilitiesAsync(longitude, latitude, type, searchRadius, cancellationToken);
                    var result = facilities.Select(f => new
                    {
                        id = f.Id,
                        name = f.Name,
                        address = f.Address,
                        distance = f.Distance,
                        location = f.Location,
                    });
                    return Results.Json(ApiResponse<object>.Ok(result));
                }
                catch (Exception exception)
                {
                    return Results.Json(ApiResponse<object>.Fail(exception.Message), statusCode: StatusCodes.Status500InternalServerError);
                }
            })
            .WithName("SearchNearbyFacilities")
            .WithSummary("查询学校周边生活设施（地铁、医院、超市等）")
            .Produces<ApiResponse<object>>();
    }
}
