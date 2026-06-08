using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using FreeSql;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using SchoolRecommender.Api.Data;
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
        .Filter.ByExcluding(e => e.Level >= LogEventLevel.Information && e.Level != LogEventLevel.Error)
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
var options = SchoolServiceOptions.FromConfiguration(builder.Configuration);

if (builder.Environment.IsDevelopment())
    builder.WebHost.UseUrls($"http://0.0.0.0:{options.Port}");

if (builder.Environment.IsProduction())
{
    builder.WebHost.UseUrls("http://*:80");
}

builder.Services.AddSingleton(options);
builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IFreeSql>(_ =>
{
    var directory = Path.GetDirectoryName(options.DatabasePath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }

    var freeSql = new FreeSqlBuilder()
        .UseConnectionString(DataType.Sqlite, $"Data Source={options.DatabasePath};")
        .UseAutoSyncStructure(true)
        .Build();

    freeSql.CodeFirst.SyncStructure<AppUser>();
    freeSql.CodeFirst.SyncStructure<EmailVerificationCode>();
    freeSql.CodeFirst.SyncStructure<Message>();
    freeSql.CodeFirst.SyncStructure<MessageReply>();
    freeSql.CodeFirst.SyncStructure<SchoolAnalysisLog>();
    freeSql.CodeFirst.SyncStructure<SiteVisitStat>();
    return freeSql;
});

// 反向代理（Nginx / Docker）转发头处理：把 X-Forwarded-For 写入 RemoteIpAddress
builder.Services.Configure<ForwardedHeadersOptions>(fh =>
{
    fh.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Docker 网桥 / 反代不在默认信任列表，清空后允许所有上游（仅当机器只暴露给可信 Nginx 时使用）
    fh.KnownIPNetworks.Clear();
    fh.KnownProxies.Clear();
    fh.ForwardLimit = 2;
});
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
    client.BaseAddress = new Uri("https://restapi.amap.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<LocalSkillScriptRunner>();
builder.Services.AddSingleton<SchoolAnalysisAgent>();
builder.Services.AddSingleton<SchoolAnalysisOrchestrator>();
builder.Services.AddSingleton<SchoolRankingService>();
builder.Services.AddSingleton<UniversityAnalysisAgent>();
builder.Services.AddSingleton<UniversityAnalysisOrchestrator>();
builder.Services.AddSingleton<UniversityRankingService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<MessageService>();
builder.Services.AddSingleton<AnalyticsService>();

var app = builder.Build();

// 必须放在其它中间件之前，才能让后续代码（限流、日志、Endpoint）拿到真实客户端 IP
app.UseForwardedHeaders();

app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();
app.UseSerilogRequestLogging();

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

var authGroup = app.MapGroup("/api/auth")
    .WithTags("Auth")
    .RequireRateLimiting("api");

authGroup.MapPost("/register/code",
    async Task<Results<Ok<ApiResponse<object>>, BadRequest<ApiResponse<object>>>> (
        [FromBody] SendRegisterCodeRequest request,
        AuthService authService,
        CancellationToken cancellationToken) =>
    {
        var result = await authService.SendRegisterCodeAsync(request, cancellationToken);
        return result.Success
            ? TypedResults.Ok(ApiResponse<object>.Ok(new { expiresInSeconds = 600 }, "验证码已发送，请查收邮箱"))
            : TypedResults.BadRequest(ApiResponse<object>.Fail(result.Message ?? "验证码发送失败"));
    })
    .WithName("SendRegisterCode")
    .WithSummary("发送邮箱注册验证码");

authGroup.MapPost("/register",
    async Task<Results<Ok<ApiResponse<AuthPayload>>, BadRequest<ApiResponse<object>>>> (
        [FromBody] RegisterRequest request,
        AuthService authService,
        CancellationToken cancellationToken) =>
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        return result.Success && result.Payload is not null
            ? TypedResults.Ok(ApiResponse<AuthPayload>.Ok(result.Payload, "注册成功"))
            : TypedResults.BadRequest(ApiResponse<object>.Fail(result.Message ?? "注册失败"));
    })
    .WithName("Register")
    .WithSummary("使用邮箱注册");

authGroup.MapPost("/login",
    async Task<Results<Ok<ApiResponse<AuthPayload>>, BadRequest<ApiResponse<object>>>> (
        [FromBody] LoginRequest request,
        AuthService authService,
        CancellationToken cancellationToken) =>
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return result.Success && result.Payload is not null
            ? TypedResults.Ok(ApiResponse<AuthPayload>.Ok(result.Payload, "登录成功"))
            : TypedResults.BadRequest(ApiResponse<object>.Fail(result.Message ?? "登录失败"));
    })
    .WithName("Login")
    .WithSummary("使用邮箱登录");

authGroup.MapGet("/me",
    Results<Ok<ApiResponse<SessionUser>>, UnauthorizedHttpResult> (HttpContext httpContext, AuthService authService) =>
    {
        var user = authService.GetUserFromRequest(httpContext);
        return user is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(ApiResponse<SessionUser>.Ok(user));
    })
    .WithName("GetCurrentUser")
    .WithSummary("获取当前登录用户");

var messageGroup = app.MapGroup("/api/messages")
    .WithTags("Messages")
    .RequireRateLimiting("api");

messageGroup.MapGet("/",
    async Task<Ok<ApiResponse<IReadOnlyList<MessageThread>>>> (
        string? schoolId,
        MessageService messageService,
        CancellationToken cancellationToken) =>
    {
        var messages = await messageService.GetMessagesAsync(schoolId, cancellationToken);
        return TypedResults.Ok(ApiResponse<IReadOnlyList<MessageThread>>.Ok(messages));
    })
    .WithName("ListMessages")
    .WithSummary("获取留言列表，可按学校筛选");

messageGroup.MapPost("/",
    async Task<Results<Ok<ApiResponse<MessageThread>>, BadRequest<ApiResponse<object>>, UnauthorizedHttpResult>> (
        HttpContext httpContext,
        [FromBody] CreateMessageRequest request,
        AuthService authService,
        MessageService messageService,
        CancellationToken cancellationToken) =>
    {
        var user = authService.GetUserFromRequest(httpContext);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        try
        {
            var message = await messageService.CreateMessageAsync(user, request, cancellationToken);
            return TypedResults.Ok(ApiResponse<MessageThread>.Ok(message, "留言已发布"));
        }
        catch (ArgumentException exception)
        {
            return TypedResults.BadRequest(ApiResponse<object>.Fail(exception.Message));
        }
    })
    .WithName("CreateMessage")
    .WithSummary("发布学校留言");

messageGroup.MapPost("/{messageId:long}/replies",
    async Task<Results<Ok<ApiResponse<MessageReplyDto>>, BadRequest<ApiResponse<object>>, NotFound<ApiResponse<object>>, UnauthorizedHttpResult>> (
        HttpContext httpContext,
        long messageId,
        [FromBody] CreateReplyRequest request,
        AuthService authService,
        MessageService messageService,
        CancellationToken cancellationToken) =>
    {
        var user = authService.GetUserFromRequest(httpContext);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        try
        {
            var reply = await messageService.CreateReplyAsync(user, messageId, request, cancellationToken);
            return reply is null
                ? TypedResults.NotFound(ApiResponse<object>.Fail("留言不存在"))
                : TypedResults.Ok(ApiResponse<MessageReplyDto>.Ok(reply, "回复已发布"));
        }
        catch (ArgumentException exception)
        {
            return TypedResults.BadRequest(ApiResponse<object>.Fail(exception.Message));
        }
    })
    .WithName("CreateMessageReply")
    .WithSummary("回复留言");

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
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString();
        // IPv6 回环 / 映射地址处理
        if (clientIp == "::1") clientIp = "127.0.0.1";
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
    async (
        HttpContext httpContext,
        [FromBody] SearchRequest request,
        AmapService amapService,
        SchoolAnalysisOrchestrator orchestrator,
        SchoolRankingService rankingService,
        AnalyticsService analyticsService,
        CancellationToken cancellationToken) =>
    {
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

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "text/event-stream; charset=utf-8";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        async Task WriteEventAsync(string eventName, object payload)
        {
            var json = JsonSerializer.Serialize(payload, jsonOptions);
            var sb = new StringBuilder();
            sb.Append("event: ").Append(eventName).Append('\n');
            // 数据需要按行前缀，data 中不能含未转义换行
            foreach (var line in json.Split('\n'))
            {
                sb.Append("data: ").Append(line).Append('\n');
            }
            sb.Append('\n');
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            await httpContext.Response.Body.WriteAsync(bytes, cancellationToken);
            await httpContext.Response.Body.FlushAsync(cancellationToken);
        }

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

            await foreach (var (schoolId, analysis) in orchestrator.AnalyzeStreamAsync(schools, request.StudentScore, cancellationToken))
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
            await WriteEventAsync("done", new
            {
                rankings,
            });
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
    })
    .WithName("SearchSchools")
    .WithSummary("搜索附近学校并以 SSE 流式返回分析与排名");

static async Task WriteJsonErrorAsync(HttpContext context, int statusCode, string message, CancellationToken cancellationToken)
{
    context.Response.StatusCode = statusCode;
    context.Response.ContentType = "application/json; charset=utf-8";
    await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message), cancellationToken);
}

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

// ═══════════════════════════════════════════════════════════
// 大学报考分析 API
// ═══════════════════════════════════════════════════════════

var universityGroup = app.MapGroup("/api/universities")
    .WithTags("Universities")
    .RequireRateLimiting("api");

universityGroup.MapPost("/search",
    async (
        HttpContext httpContext,
        [FromBody] UniversitySearchRequest request,
        UniversityAnalysisOrchestrator orchestrator,
        UniversityRankingService rankingService,
        CancellationToken cancellationToken) =>
    {
        if (request.StudentScore is < 0 or > 750)
        {
            await WriteJsonErrorAsync(httpContext, StatusCodes.Status400BadRequest, "请输入有效的高考分数 (0-750)", cancellationToken);
            return;
        }

        if (request.UniversityNames is not { Count: > 0 })
        {
            await WriteJsonErrorAsync(httpContext, StatusCodes.Status400BadRequest, "请至少提供一所意向大学", cancellationToken);
            return;
        }

        if (request.UniversityNames.Count > 10)
        {
            await WriteJsonErrorAsync(httpContext, StatusCodes.Status400BadRequest, "最多同时分析 10 所大学", cancellationToken);
            return;
        }

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "text/event-stream; charset=utf-8";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        async Task WriteEventAsync(string eventName, object payload)
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
            await httpContext.Response.Body.WriteAsync(bytes, cancellationToken);
            await httpContext.Response.Body.FlushAsync(cancellationToken);
        }

        try
        {
            await WriteEventAsync("meta", new
            {
                studentScore = request.StudentScore,
                universityNames = request.UniversityNames,
                preferredMajors = request.PreferredMajors,
                totalCount = request.UniversityNames.Count,
            });

            var analyses = new List<UniversityAnalysis>();

            await foreach (var (universityName, analysis) in orchestrator.AnalyzeStreamAsync(
                request.UniversityNames, request.StudentScore, request.PreferredMajors, cancellationToken))
            {
                analyses.Add(analysis);
                await WriteEventAsync("analysis", new
                {
                    universityName,
                    analysis,
                });
            }

            var rankings = rankingService.RankUniversities(analyses, request.StudentScore);
            await WriteEventAsync("done", new
            {
                rankings,
            });
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
    })
    .WithName("SearchUniversities")
    .WithSummary("根据高考成绩和意向大学，分析录取分数线与专业信息，推荐报考排名");

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