using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Options;
using SchoolRecommender.Api.Services;
using SchoolRecommender.Api.Utils;

EnvFileLoader.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Verbose)
    .MinimumLevel.Override("Microsoft.SemanticKernel", LogEventLevel.Verbose)
    .MinimumLevel.Override("System.Net.Http", LogEventLevel.Verbose)
    // 降噪
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Logger(lc => lc
        .Filter.ByExcluding(e => e.Level >= LogEventLevel.Error)
        .WriteTo.File(
            path: "logs/info-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error)
        .WriteTo.File(
            path: "logs/error-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
var options = SchoolServiceOptions.FromConfiguration(builder.Configuration, builder.Environment.ContentRootPath);

builder.WebHost.UseUrls($"http://0.0.0.0:{options.Port}");

if (builder.Environment.IsProduction())
{
    builder.WebHost.UseUrls("http://*:80");
}

builder.Services.AddSingleton(options);
builder.Services.AddProblemDetails();
builder.Services.AddCors(cors =>
{
    cors.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddRateLimiter(rateLimiter =>
{
    rateLimiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rateLimiter.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});
builder.Services.AddOpenApi("v1");
builder.Services.AddHttpClient<AmapService>(client =>
{
    client.BaseAddress = new Uri("https://restapi.amap.com/v3/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<LocalSkillScriptRunner>();
builder.Services.AddSingleton<SchoolAnalysisAgent>();
builder.Services.AddSingleton<SchoolAnalysisOrchestrator>();
builder.Services.AddSingleton<SchoolRankingService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();
app.UseSerilogRequestLogging();

app.MapOpenApi("/openapi/{documentName}.json");
app.MapScalarApiReference("/scalar", scalar =>
{
    scalar.WithTitle("School Recommender API");
    scalar.WithTheme(ScalarTheme.BluePlanet);
    scalar.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

app.MapGet("/api/health", () =>
        TypedResults.Ok(new { status = "ok", timestamp = DateTimeOffset.UtcNow }))
    .WithTags("System")
    .WithName("Health")
    .WithSummary("健康检查");

var locationGroup = app.MapGroup("/api/location")
    .WithTags("Location")
    .RequireRateLimiting("api");

locationGroup.MapPost("/geocode",
    async Task<Results<Ok<ApiResponse<GeoLocationResponse>>, BadRequest<ApiResponse<object>>, ProblemHttpResult>> (
        [FromBody] GeocodeRequest request,
        AmapService amapService,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return TypedResults.BadRequest(ApiResponse<object>.Fail("请提供有效的地址"));
        }

        var result = await amapService.GeocodeAsync(request.Address, cancellationToken);
        return TypedResults.Ok(ApiResponse<GeoLocationResponse>.Ok(result));
    })
    .WithName("GeocodeAddress")
    .WithSummary("地址转坐标");

locationGroup.MapPost("/reverse",
    async Task<Results<Ok<ApiResponse<ReverseGeocodeResponse>>, BadRequest<ApiResponse<object>>, ProblemHttpResult>> (
        [FromBody] ReverseGeocodeRequest request,
        AmapService amapService,
        CancellationToken cancellationToken) =>
    {
        if (!GeoValidator.IsValidLatitude(request.Latitude) || !GeoValidator.IsValidLongitude(request.Longitude))
        {
            return TypedResults.BadRequest(ApiResponse<object>.Fail("请提供有效的经纬度"));
        }

        var result = await amapService.ReverseGeocodeAsync(request.Longitude, request.Latitude, cancellationToken);
        return TypedResults.Ok(ApiResponse<ReverseGeocodeResponse>.Ok(result));
    })
    .WithName("ReverseGeocode")
    .WithSummary("坐标转地址");

locationGroup.MapGet("/ip",
    async Task<Ok<ApiResponse<IpLocationResponse>>> (
        HttpContext httpContext,
        AmapService amapService,
        CancellationToken cancellationToken) =>
    {
        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].ToString();
        var clientIp = string.IsNullOrWhiteSpace(forwarded)
            ? httpContext.Connection.RemoteIpAddress?.ToString()
            : forwarded.Split(',')[0].Trim();
        try
        {
            var result = await amapService.IpLocateAsync(clientIp, cancellationToken);
            return TypedResults.Ok(ApiResponse<IpLocationResponse>.Ok(result));
        }
        catch (Exception exception)
        {
            return TypedResults.Ok(ApiResponse<IpLocationResponse>.Fail(exception.Message, fallbackToBrowser: true));
        }
    })
    .WithName("LocateByIp")
    .WithSummary("根据访问者 IP 自动定位");

var schoolGroup = app.MapGroup("/api/schools")
    .WithTags("Schools")
    .RequireRateLimiting("api");

schoolGroup.MapPost("/search",
    async Task<Results<Ok<ApiResponse<SearchResponseData>>, BadRequest<ApiResponse<object>>, ProblemHttpResult>> (
        [FromBody] SearchRequest request,
        AmapService amapService,
        SchoolAnalysisOrchestrator orchestrator,
        SchoolRankingService rankingService,
        CancellationToken cancellationToken) =>
    {
        if (!GeoValidator.IsValidLatitude(request.Latitude) || !GeoValidator.IsValidLongitude(request.Longitude))
        {
            return TypedResults.BadRequest(ApiResponse<object>.Fail("经纬度范围无效"));
        }

        if (!SchoolTypes.IsValid(request.SchoolType))
        {
            return TypedResults.BadRequest(ApiResponse<object>.Fail($"无效的学校类型: {request.SchoolType}"));
        }

        var radius = request.Radius is > 0 and <= 20000 ? request.Radius.Value : 5000;
        var location = await amapService.ReverseGeocodeAsync(request.Longitude, request.Latitude, cancellationToken);
        var pois = await amapService.SearchNearbySchoolsAsync(request.Longitude, request.Latitude, request.SchoolType, radius, cancellationToken);

        if (pois.Count == 0)
        {
            var empty = new SearchResponseData(
                new SearchLocation(location.Address, request.Latitude, request.Longitude),
                [],
                radius,
                0);

            return TypedResults.Ok(ApiResponse<SearchResponseData>.Ok(empty, "附近未找到相关学校，请尝试扩大搜索范围"));
        }

        var schools = pois.Select(poi => SchoolMapper.FromPoi(poi, request.SchoolType)).ToArray();
        var analyses = await orchestrator.AnalyzeAsync(schools, cancellationToken);
        var rankings = rankingService.RankSchools(schools, analyses);

        var response = new SearchResponseData(
            new SearchLocation(location.Address, request.Latitude, request.Longitude),
            rankings,
            radius,
            rankings.Count);

        return TypedResults.Ok(ApiResponse<SearchResponseData>.Ok(response));
    })
    .WithName("SearchSchools")
    .WithSummary("搜索附近学校并返回分析排名");

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

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

internal static class GeoValidator
{
    public static bool IsValidLatitude(double value) => value is >= -90 and <= 90;

    public static bool IsValidLongitude(double value) => value is >= -180 and <= 180;
}