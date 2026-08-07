using Microsoft.AspNetCore.Http.HttpResults;
using Scalar.AspNetCore;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Options;
using SchoolRecommender.Api.Services;

namespace SchoolRecommender.Api.Endpoints;

internal static class SystemEndpoints
{
    public static void MapSystemEndpoints(this WebApplication app)
    {
        app.MapPost("/api/analytics/visit",
            async Task<Ok<ApiResponse<object>>> (
                HttpContext httpContext,
                AnalyticsService analyticsService,
                CancellationToken cancellationToken) =>
            {
                var path = httpContext.Request.Query["path"].FirstOrDefault();
                await analyticsService.RecordVisitAsync(string.IsNullOrWhiteSpace(path) ? "/" : path, cancellationToken);
                return TypedResults.Ok(ApiResponse<object>.Ok(new { tracked = true }));
            })
            .WithTags("Analytics")
            .WithName("TrackVisit")
            .WithSummary("记录站点访问次数");

        app.MapOpenApi("/openapi/{documentName}.json");
        app.MapScalarApiReference("/scalar", scalar =>
        {
            scalar.WithTitle("School Recommender API");
            scalar.WithTheme(ScalarTheme.BluePlanet);
            scalar.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
        });

        app.MapGet("/api/health", (SchoolServiceOptions svcOptions) =>
                TypedResults.Ok(new
                {
                    status = "ok",
                    timestamp = DateTimeOffset.UtcNow,
                    configuration = new
                    {
                        model = svcOptions.DashScopeModel,
                        dashScopeNetworkTimeoutSeconds = (int)svcOptions.DashScopeNetworkTimeout.TotalSeconds,
                        dashScopeRateLimitMaxRetries = svcOptions.DashScopeRateLimitMaxRetries,
                        dashScopeRateLimitBaseDelayMs = (int)svcOptions.DashScopeRateLimitBaseDelay.TotalMilliseconds,
                        dashScopeRateLimitMaxDelayMs = (int)svcOptions.DashScopeRateLimitMaxDelay.TotalMilliseconds,
                        dashScopeMinRequestIntervalMs = (int)svcOptions.DashScopeMinRequestInterval.TotalMilliseconds,
                        maxConcurrency = svcOptions.AnalysisMaxConcurrency,
                        amapPageSize = svcOptions.AmapPageSize,
                        skillTimeoutSeconds = (int)svcOptions.SkillTimeout.TotalSeconds,
                        databasePath = svcOptions.DatabasePath,
                        smtpConfigured = !string.IsNullOrWhiteSpace(svcOptions.SmtpHost),
                    },
                    environment = new
                    {
                        dotnetVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                        osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                        processorCount = Environment.ProcessorCount,
                        workingSetMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0),
                    },
                }))
            .WithTags("System")
            .WithName("Health")
            .WithSummary("健康检查与系统信息");
    }
}
