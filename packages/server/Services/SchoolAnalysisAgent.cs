using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Options;

namespace SchoolRecommender.Api.Services;

/// <summary>
/// 学校公开内容分析 Agent，分两个明确阶段执行：
/// <list type="number">
///   <item>调研阶段：通过 <see cref="LocalSkillScriptRunner"/> 并行调用本地技能脚本，
///         分别传入学校名称参数搜索微信公众号与小红书内容。</item>
///   <item>分析阶段：将采集结果连同学校基本信息一起发送给 AI，输出结构化评分 JSON。</item>
/// </list>
/// </summary>
public sealed class SchoolAnalysisAgent
{
    private readonly IChatClient _chatClient;
    private readonly LocalSkillScriptRunner _skillRunner;
    private readonly ILogger<SchoolAnalysisAgent> _logger;

    public SchoolAnalysisAgent(
        SchoolServiceOptions options,
        LocalSkillScriptRunner skillRunner,
        ILogger<SchoolAnalysisAgent> logger)
    {
        if (string.IsNullOrWhiteSpace(options.DashScopeApiKey))
            throw new InvalidOperationException("未配置 DASHSCOPE_API_KEY 或 OPENAI_API_KEY，无法初始化分析 Agent");

        var fixerHandler = new DashScopeResponseFixerHandler();
        var httpClient = new HttpClient(fixerHandler);

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(options.DashScopeApiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(options.DashScopeBaseUrl, UriKind.Absolute),
                Transport = new HttpClientPipelineTransport(httpClient),
            });

        // FunctionCallArgumentNormalizerChatClient 修复 DashScope/Qwen 将 function
        // arguments 序列化为数组而非对象的问题
        _chatClient = new FunctionCallArgumentNormalizerChatClient(
            openAiClient.GetChatClient(options.DashScopeModel).AsIChatClient());

        _skillRunner = skillRunner;
        _logger = logger;
    }

    // ─── 公共入口 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 分析单所学校的公开舆情内容，返回结构化评分结果。
    /// </summary>
    public async Task<SchoolAnalysis> AnalyzeAsync(School school, CancellationToken cancellationToken)
    {
        // 第一阶段：并行调用本地技能脚本，采集公开内容
        var research = await GatherResearchDataAsync(school, cancellationToken);

        // 第二阶段：将采集结果交给 AI 分析，输出结构化 JSON
        return await RunAnalysisAsync(school, research, cancellationToken);
    }

    // ─── 第一阶段：调研 ───────────────────────────────────────────────────────

    /// <summary>
    /// 并行执行微信公众号与小红书两个技能脚本，以学校名称为搜索参数采集公开内容。
    /// 任一脚本失败时不抛出异常，由脚本运行器返回错误描述字符串，
    /// 后续分析阶段会据此体现为"样本不足"。
    /// </summary>
    private async Task<SchoolResearchData> GatherResearchDataAsync(
        School school,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始采集学校公开内容: {SchoolName}", school.Name);

        // 以学校名称为 query/keyword 参数，并行调用两个 skill 脚本
        var wechatTask = _skillRunner.RunWechatSearchAsync(
            query: school.Name,
            cancellationToken: cancellationToken);

        var xiaohongshuTask = _skillRunner.RunXiaohongshuSearchAsync(
            keyword: school.Name,
            cancellationToken: cancellationToken);

        await Task.WhenAll(wechatTask, xiaohongshuTask);

        var wechatContent = await wechatTask;
        var xiaohongshuContent = await xiaohongshuTask;

        _logger.LogInformation(
            "公开内容采集完成: {SchoolName} | 微信: {WechatChars} 字符, 小红书: {XhsChars} 字符",
            school.Name, wechatContent.Length, xiaohongshuContent.Length);

        return new SchoolResearchData(wechatContent, xiaohongshuContent);
    }

    // ─── 第二阶段：AI 分析 ────────────────────────────────────────────────────

    /// <summary>
    /// 将学校信息与已采集内容组装为分析提示，调用 AI 返回结构化 JSON 评分。
    /// 解析失败时使用保守兜底结果。
    /// </summary>
    private async Task<SchoolAnalysis> RunAnalysisAsync(
        School school,
        SchoolResearchData research,
        CancellationToken cancellationToken)
    {
        var messages = BuildAnalysisMessages(school, research);

        try
        {
            var response = await _chatClient.GetResponseAsync(messages, options: null, cancellationToken: cancellationToken);
            var raw = response.Messages.LastOrDefault()?.Text ?? string.Empty;

            _logger.LogInformation("AI 分析原始返回: {SchoolName} => {RawResponse}", school.Name, raw);

            var json = ExtractJson(raw)
                ?? throw new InvalidOperationException("AI 未返回可解析的 JSON");

            return JsonSerializer.Deserialize<SchoolAnalysis>(json, JsonOptions)
                ?? CreateFallbackAnalysis();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "学校分析失败，使用保守兜底结果: {SchoolName}", school.Name);
            return CreateFallbackAnalysis();
        }
    }

    // ─── 提示构建 ─────────────────────────────────────────────────────────────

    private static IList<ChatMessage> BuildAnalysisMessages(School school, SchoolResearchData research) =>
    [
        new ChatMessage(ChatRole.System, SystemPrompt),
        new ChatMessage(ChatRole.User, BuildUserPrompt(school, research)),
    ];

    private static string BuildUserPrompt(School school, SchoolResearchData research)
    {
        var wechatSection = FormatResearchSection(research.WechatContent);
        var xhsSection = FormatResearchSection(research.XiaohongshuContent);

        return $$"""
请根据以下已采集的公开内容，对该学校进行保守、可解释的择校评分分析。

【学校基本信息】
名称：{{school.Name}}
地址：{{school.Address}}
类型：{{SchoolTypes.DisplayName(school.Type)}}
距用户位置：{{school.Distance}} 米

【微信公众号搜索结果】
{{wechatSection}}

【小红书搜索结果】
{{xhsSection}}

评分要求：
- 样本稀少时给保守低分（建议 30-50 区间），不要给虚高分
- sources 只列实际检索到的来源，无来源时返回空数组
- lastPostDate 优先使用真实日期，无日期时使用今天日期
- 不编造任何信息

只返回如下格式的严格 JSON（不含 Markdown 代码块，不含解释文字）：
{
  "activityScore": 0-100 的整数,
  "lastPostDate": "YYYY-MM-DD",
  "reputationScore": 0-100 的整数,
  "reputationSummary": "一句话总结",
  "contentQualityScore": 0-100 的整数,
  "teachingStaffScore": 0-100 的整数,
  "teachingStaffDesc": "一句话",
  "awardsCount": 整数,
  "awardsDetail": ["字符串"],
  "studentActivityScore": 0-100 的整数,
  "studentActivityDesc": "一句话",
  "sources": [
    { "platform": "微信公众号或小红书", "accountName": "可选", "postCount": 整数, "lastPostDate": "可选", "url": "可选" }
  ]
}
""";
    }

    /// <summary>
    /// 将脚本输出内容格式化为提示节，空结果或错误结果显示为"无数据"提示。
    /// </summary>
    private static string FormatResearchSection(string content)
    {
        var hasContent = !string.IsNullOrWhiteSpace(content)
            && !content.StartsWith("Error:", StringComparison.OrdinalIgnoreCase)
            && content != "(no output)";

        return hasContent ? content : "（无数据 — 评分请体现为样本不足）";
    }

    // ─── 辅助方法 & 常量 ──────────────────────────────────────────────────────

    private static string? ExtractJson(string raw)
    {
        // 优先尝试提取 ```json ... ``` 代码块
        var codeBlockMatch = System.Text.RegularExpressions.Regex.Match(
            raw,
            """```json\s*(?<json>[\s\S]*?)\s*```""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (codeBlockMatch.Success)
            return codeBlockMatch.Groups["json"].Value.Trim();

        // 回退：提取第一个 { 到最后一个 } 之间的内容
        var firstBrace = raw.IndexOf('{');
        var lastBrace = raw.LastIndexOf('}');
        return firstBrace >= 0 && lastBrace > firstBrace
            ? raw[firstBrace..(lastBrace + 1)]
            : null;
    }

    private static SchoolAnalysis CreateFallbackAnalysis() => new(
        ActivityScore: 45,
        LastPostDate: DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
        ReputationScore: 45,
        ReputationSummary: "公开样本不足，建议结合学校开放日、家长访谈和官方资料进一步核验。",
        ContentQualityScore: 40,
        TeachingStaffScore: 45,
        TeachingStaffDesc: "仅依据公开内容难以完整判断师资，建议结合官方信息进一步核验。",
        AwardsCount: 0,
        AwardsDetail: [],
        StudentActivityScore: 42,
        StudentActivityDesc: "学生活动公开信息较少。",
        Sources: []);

    private const string SystemPrompt =
        """
        你是一个专注于学校舆情的分析专家，基于已采集的公开内容进行保守、可解释的择校分析。

        核心原则：
        - 只根据提供的内容进行分析，不编造任何信息
        - 数据不足时给出保守低分，并在总结字段中说明样本不足
        - 只返回严格 JSON，不包含 Markdown 代码块或解释文字
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>第一阶段调研结果：微信与小红书脚本的原始输出。</summary>
    private sealed record SchoolResearchData(string WechatContent, string XiaohongshuContent);
}