using System.Threading.RateLimiting;
using FreeSql;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Serilog.Events;
using SchoolRecommender.Api.Data;
using SchoolRecommender.Api.Endpoints;
using SchoolRecommender.Api.Options;
using SchoolRecommender.Api.Services;
using SchoolRecommender.Api.Utils;
using StackExchange.Redis;

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
        .Filter.ByExcluding(e => e.Level >= LogEventLevel.Debug)
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
    freeSql.CodeFirst.SyncStructure<SiteMessage>();
    freeSql.CodeFirst.SyncStructure<SiteMessageReply>();
    freeSql.CodeFirst.SyncStructure<SchoolAnalysisLog>();
    freeSql.CodeFirst.SyncStructure<SiteVisitStat>();
    freeSql.CodeFirst.SyncStructure<AnalysisHistory>();
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
builder.Services.AddSingleton<AgentProgressContext>();
builder.Services.AddSingleton<LocalSkillScriptRunner>();
builder.Services.AddSingleton<SchoolAnalysisAgent>();
builder.Services.AddSingleton<SchoolAnalysisOrchestrator>();
builder.Services.AddSingleton<SchoolRankingService>();
builder.Services.AddSingleton<UniversityAnalysisAgent>();
builder.Services.AddSingleton<UniversityAnalysisOrchestrator>();
builder.Services.AddSingleton<UniversityRankingService>();
builder.Services.AddSingleton<HighSchoolRecommendAgent>();
builder.Services.AddSingleton<HighSchoolRankingService>();
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options.RedisConnectionString));
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<MessageService>();
builder.Services.AddSingleton<AnalyticsService>();
builder.Services.AddSingleton<HistoryService>();

var app = builder.Build();

// 必须放在其它中间件之前，才能让后续代码（限流、日志、Endpoint）拿到真实客户端 IP
app.UseForwardedHeaders();

app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();
app.UseSerilogRequestLogging();

app.MapApplicationEndpoints();

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
