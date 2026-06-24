namespace SchoolRecommender.Api.Options;

public sealed record SchoolServiceOptions(
    int Port,
    string AmapApiKey,
    string AmapSecurityKey,
    string DashScopeApiKey,
    string DashScopeBaseUrl,
    TimeSpan DashScopeNetworkTimeout,
    int DashScopeRateLimitMaxRetries,
    TimeSpan DashScopeRateLimitBaseDelay,
    TimeSpan DashScopeRateLimitMaxDelay,
    TimeSpan DashScopeMinRequestInterval,
    bool DashScopeUseResponsesApi,
    string DashScopeModel,
    string NodeCommand,
    string PythonCommand,
    TimeSpan SkillTimeout,
    string SkillRootPath,
    string DatabasePath,
    string SmtpHost,
    int SmtpPort,
    bool SmtpEnableSsl,
    string SmtpUserName,
    string SmtpPassword,
    string SmtpFromEmail,
    string SmtpFromName,
    int AnalysisMaxConcurrency,
    int AmapPageSize,
    string RedisConnectionString)
{
    public static SchoolServiceOptions FromConfiguration(IConfiguration configuration)
    {
        var timeoutSeconds = int.TryParse(configuration["SKILL_TIMEOUT_SECONDS"], out var value) ? value : 60;
        var dashScopeNetworkTimeoutSeconds = int.TryParse(
            configuration["DASHSCOPE_NETWORK_TIMEOUT_SECONDS"] ?? configuration["OPENAI_NETWORK_TIMEOUT_SECONDS"],
            out var networkTimeoutSeconds)
            ? networkTimeoutSeconds
            : 180;
        var dashScopeRateLimitMaxRetries = Math.Clamp(
            int.TryParse(configuration["DASHSCOPE_RATE_LIMIT_MAX_RETRIES"], out var retryCount) ? retryCount : 4,
            0,
            10);
        var dashScopeRateLimitBaseDelayMs = Math.Clamp(
            int.TryParse(configuration["DASHSCOPE_RATE_LIMIT_BASE_DELAY_MS"], out var baseDelayMs) ? baseDelayMs : 2000,
            200,
            60000);
        var dashScopeRateLimitMaxDelayMs = Math.Clamp(
            int.TryParse(configuration["DASHSCOPE_RATE_LIMIT_MAX_DELAY_MS"], out var maxDelayMs) ? maxDelayMs : 15000,
            dashScopeRateLimitBaseDelayMs,
            120000);
        var dashScopeMinRequestIntervalMs = Math.Clamp(
            int.TryParse(configuration["DASHSCOPE_MIN_REQUEST_INTERVAL_MS"], out var minRequestIntervalMs) ? minRequestIntervalMs : 800,
            0,
            10000);
        var maxConcurrency = int.TryParse(configuration["ANALYSIS_MAX_CONCURRENCY"], out var concurrency) ? concurrency : 5;
        var configuredBaseUrl = configuration["DASHSCOPE_BASE_URL"] ?? configuration["OPENAI_BASE_URL"];
        var apiStyle = configuration["DASHSCOPE_API_STYLE"] ?? configuration["OPENAI_API_STYLE"];
        var useResponsesApi = ShouldUseResponsesApi(configuredBaseUrl, apiStyle);

        return new SchoolServiceOptions(
            Port: int.TryParse(configuration["PORT"], out var port) ? port : 3001,
            AmapApiKey: configuration["AMAP_API_KEY"] ?? string.Empty,
            AmapSecurityKey: configuration["SK"] ?? string.Empty,
            DashScopeApiKey: configuration["DASHSCOPE_API_KEY"] ?? configuration["OPENAI_API_KEY"] ?? string.Empty,
            DashScopeBaseUrl: NormalizeOpenAiEndpoint(configuredBaseUrl) ?? "https://dashscope.aliyuncs.com/compatible-mode/v1",
            DashScopeNetworkTimeout: TimeSpan.FromSeconds(Math.Clamp(dashScopeNetworkTimeoutSeconds, 30, 600)),
            DashScopeRateLimitMaxRetries: dashScopeRateLimitMaxRetries,
            DashScopeRateLimitBaseDelay: TimeSpan.FromMilliseconds(dashScopeRateLimitBaseDelayMs),
            DashScopeRateLimitMaxDelay: TimeSpan.FromMilliseconds(dashScopeRateLimitMaxDelayMs),
            DashScopeMinRequestInterval: TimeSpan.FromMilliseconds(dashScopeMinRequestIntervalMs),
            DashScopeUseResponsesApi: useResponsesApi,
            DashScopeModel: configuration["DASHSCOPE_MODEL"] ?? configuration["OPENAI_CHAT_MODEL_NAME"] ?? "qwen3.6-plus",
            NodeCommand: configuration["NODE_COMMAND"] ?? "node",
            PythonCommand: configuration["PYTHON_COMMAND"] ?? "python",
            SkillTimeout: TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 15)),
            SkillRootPath: ResolveSkillRootPath(configuration),
            DatabasePath: ResolveDatabasePath(configuration),
            SmtpHost: configuration["SMTP_HOST"] ?? string.Empty,
            SmtpPort: Math.Clamp(int.TryParse(configuration["SMTP_PORT"], out var smtpPort) ? smtpPort : 587, 1, 65535),
            SmtpEnableSsl: !bool.TryParse(configuration["SMTP_ENABLE_SSL"], out var smtpEnableSsl) || smtpEnableSsl,
            SmtpUserName: configuration["SMTP_USERNAME"] ?? string.Empty,
            SmtpPassword: configuration["SMTP_PASSWORD"] ?? string.Empty,
            SmtpFromEmail: configuration["SMTP_FROM_EMAIL"] ?? configuration["SMTP_USERNAME"] ?? string.Empty,
            SmtpFromName: configuration["SMTP_FROM_NAME"] ?? "学校推荐系统",
            AnalysisMaxConcurrency: Math.Clamp(maxConcurrency, 1, 32),
            AmapPageSize: Math.Clamp(int.TryParse(configuration["AMAP_PAGE_SIZE"], out var pageSize) ? pageSize : 25, 1, 25),
            RedisConnectionString: configuration["REDIS_CONNECTION_STRING"] ?? "localhost:6379");
    }

    private static bool ShouldUseResponsesApi(string? configuredBaseUrl, string? apiStyle)
    {
        if (!string.IsNullOrWhiteSpace(apiStyle))
        {
            return string.Equals(apiStyle.Trim(), "responses", StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return false;
        }

        return configuredBaseUrl.Trim().TrimEnd('/').EndsWith("/responses", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeOpenAiEndpoint(string? configuredBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return null;
        }

        var trimmed = configuredBaseUrl.Trim().Trim().Trim('"').TrimEnd('/');
        return trimmed.EndsWith("/responses", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^"/responses".Length]
            : trimmed;
    }

    private static string ResolveSkillRootPath(IConfiguration configuration)
    {
        // 优先使用配置值（支持绝对或相对路径）
        var configured = configuration["SKILL_ROOT_PATH"];

        // 程序所在目录（发布后即可执行文件目录），相对它解析 skills 路径，
        // 避免受到工作目录 / ContentRoot 影响
        var baseDir = AppContext.BaseDirectory;

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(baseDir, configured));
        }

        return Path.Combine(baseDir, "skills");
    }

    private static string ResolveDatabasePath(IConfiguration configuration)
    {
        var configured = configuration["DATABASE_PATH"];
        var baseDir = AppContext.BaseDirectory;
        var path = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(baseDir, "data", "school-recommender.db")
            : configured.Trim().Trim('"');

        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(baseDir, path));
    }
}