using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Services;

namespace SchoolRecommender.Api.Endpoints;

internal static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(this WebApplication app)
    {
        var historyGroup = app.MapGroup("/api/history")
            .WithTags("History")
            .RequireRateLimiting("api");

        historyGroup.MapGet("",
            async (
                HttpContext httpContext,
                HistoryService historyService,
                AuthService authService,
                int page = 1,
                int pageSize = 20,
                CancellationToken cancellationToken = default) =>
            {
                if (await authService.GetUserFromRequestAsync(httpContext) is not { } user)
                {
                    return Results.Json(ApiResponse<object>.Fail("请先登录"), statusCode: StatusCodes.Status401Unauthorized);
                }

                var items = await historyService.ListAsync(user.Id, page, pageSize, cancellationToken);
                var total = await historyService.CountAsync(user.Id, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { items, total, page, pageSize }));
            })
            .WithName("GetAnalysisHistory")
            .WithSummary("获取当前用户的历史分析记录列表");

        historyGroup.MapGet("/{id:long}",
            async (
                HttpContext httpContext,
                long id,
                HistoryService historyService,
                AuthService authService,
                CancellationToken cancellationToken) =>
            {
                if (await authService.GetUserFromRequestAsync(httpContext) is not { } user)
                {
                    return Results.Json(ApiResponse<object>.Fail("请先登录"), statusCode: StatusCodes.Status401Unauthorized);
                }

                var detail = await historyService.GetDetailAsync(id, user.Id, cancellationToken);
                if (detail is null)
                {
                    return Results.Json(ApiResponse<object>.Fail("记录不存在"), statusCode: StatusCodes.Status404NotFound);
                }

                return Results.Ok(ApiResponse<AnalysisHistoryDetail>.Ok(detail));
            })
            .WithName("GetAnalysisHistoryDetail")
            .WithSummary("获取单条历史分析记录详情（含完整结果）");

        historyGroup.MapDelete("/{id:long}",
            async (
                HttpContext httpContext,
                long id,
                HistoryService historyService,
                AuthService authService,
                CancellationToken cancellationToken) =>
            {
                if (await authService.GetUserFromRequestAsync(httpContext) is not { } user)
                {
                    return Results.Json(ApiResponse<object>.Fail("请先登录"), statusCode: StatusCodes.Status401Unauthorized);
                }

                var deleted = await historyService.DeleteAsync(id, user.Id, cancellationToken);
                return deleted
                    ? Results.Ok(ApiResponse<object>.Ok(new { }))
                    : Results.Json(ApiResponse<object>.Fail("记录不存在"), statusCode: StatusCodes.Status404NotFound);
            })
            .WithName("DeleteAnalysisHistory")
            .WithSummary("删除单条历史分析记录");

        historyGroup.MapDelete("",
            async (
                HttpContext httpContext,
                HistoryService historyService,
                AuthService authService,
                CancellationToken cancellationToken) =>
            {
                if (await authService.GetUserFromRequestAsync(httpContext) is not { } user)
                {
                    return Results.Json(ApiResponse<object>.Fail("请先登录"), statusCode: StatusCodes.Status401Unauthorized);
                }

                await historyService.DeleteAllAsync(user.Id, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { }));
            })
            .WithName("DeleteAllAnalysisHistory")
            .WithSummary("清空当前用户所有历史分析记录");
    }
}
