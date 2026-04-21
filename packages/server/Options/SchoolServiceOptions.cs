namespace SchoolRecommender.Api.Options;

public sealed record SchoolServiceOptions(
    int Port,
    string AmapApiKey,
    string AmapSecurityKey,
    string DashScopeApiKey,
    string DashScopeBaseUrl,
    string DashScopeModel,
    string NodeCommand,
    string PythonCommand,
    TimeSpan SkillTimeout,
    string SkillRootPath)
{
    public static SchoolServiceOptions FromConfiguration(IConfiguration configuration, string contentRootPath)
    {
        var timeoutSeconds = int.TryParse(configuration["SKILL_TIMEOUT_SECONDS"], out var value) ? value : 90;

        return new SchoolServiceOptions(
            Port: int.TryParse(configuration["PORT"], out var port) ? port : 3001,
            AmapApiKey: configuration["AMAP_API_KEY"] ?? string.Empty,
            AmapSecurityKey: configuration["SK"] ?? string.Empty,
            DashScopeApiKey: configuration["DASHSCOPE_API_KEY"] ?? configuration["OPENAI_API_KEY"] ?? string.Empty,
            DashScopeBaseUrl: configuration["DASHSCOPE_BASE_URL"] ?? configuration["OPENAI_BASE_URL"] ?? "https://dashscope.aliyuncs.com/compatible-mode/v1",
            DashScopeModel: configuration["DASHSCOPE_MODEL"] ?? configuration["OPENAI_CHAT_MODEL_NAME"] ?? "qwen3.6-plus",
            NodeCommand: configuration["NODE_COMMAND"] ?? "node",
            PythonCommand: configuration["PYTHON_COMMAND"] ?? "python",
            SkillTimeout: TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 15)),
            SkillRootPath: Path.Combine(contentRootPath, "skills"));
    }
}