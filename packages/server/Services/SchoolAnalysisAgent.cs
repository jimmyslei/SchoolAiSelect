using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Options;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using System.Text.Json;

namespace SchoolRecommender.Api.Services;

/// <summary>
/// 学校公开内容分析 Agent。
/// 通过 <see cref="AgentSkillsProvider"/> 从本地 skills 目录自动加载技能工具，
/// 由 <see cref="ChatClientAgent"/> 托管完整的 tool-call 循环：AI 自主决定调用
/// wechat-article-search / xiaohongshu-search 等技能采集公开内容，再输出结构化评分 JSON。
/// </summary>
public sealed class SchoolAnalysisAgent
{
    private readonly ChatClientAgent _agent;
    private readonly ILogger<SchoolAnalysisAgent> _logger;

    public SchoolAnalysisAgent(
        SchoolServiceOptions options,
        LocalSkillScriptRunner skillScriptRunner,
        ILoggerFactory loggerFactory,
        ILogger<SchoolAnalysisAgent> logger)
    {
        if (string.IsNullOrWhiteSpace(options.DashScopeApiKey))
            throw new InvalidOperationException("未配置 DASHSCOPE_API_KEY 或 OPENAI_API_KEY，无法初始化分析 Agent");

        if (!Directory.Exists(options.SkillRootPath))
            throw new DirectoryNotFoundException($"未找到 skills 目录: {options.SkillRootPath}");

        var fixerHandler = new DashScopeResponseFixerHandler();
        var httpClient = new HttpClient(fixerHandler);

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(options.DashScopeApiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(options.DashScopeBaseUrl, UriKind.Absolute),
                Transport = new HttpClientPipelineTransport(httpClient),
            });

        // AgentSkillsProvider 从 skills 目录自动发现 SKILL.md，把技能说明注入系统提示，
        // 但它生成的 run_skill_script 工具不带参数 schema —— 某些模型（DashScope/Qwen）
        // 无法据此传入 query/keyword，导致"只加载 skill 却不执行脚本"。
        // 因此我们保留 skillsProvider 做说明展示，但**额外把两个脚本包装成显式 AIFunction**，
        // 带明确的参数 schema 让模型可靠传参。
        var skillsProvider = new AgentSkillsProvider(
            options.SkillRootPath,
            skillScriptRunner.RunAsync,
            loggerFactory: loggerFactory);

        var searchWechatTool = AIFunctionFactory.Create(
            (string query, int? num, CancellationToken ct)
                => skillScriptRunner.SearchWechatArticlesAsync(query, num, ct),
            name: "search_wechat_articles",
            description: "搜索微信公众号文章。参数 query 为必填的搜索关键词（例如学校名称），可选 num 为返回条数（默认 10，最大 50）。返回 JSON 字符串，包含文章标题、链接、摘要、发布时间、来源公众号。");

        var searchXiaohongshuTool = AIFunctionFactory.Create(
            (string keyword, int? limit, CancellationToken ct)
                => skillScriptRunner.SearchXiaohongshuNotesAsync(keyword, limit, ct),
            name: "search_xiaohongshu_notes",
            description: "搜索小红书（xiaohongshu/rednote）笔记。参数 keyword 为必填的搜索关键词（例如学校名称），可选 limit 为返回条数（默认 10）。返回 JSON 字符串，包含笔记 id、标题、作者、点赞/收藏/评论数。");

        // FunctionCallArgumentNormalizerChatClient 修复 DashScope/Qwen 将 function
        // arguments 序列化为数组而非对象的问题
        _agent = new FunctionCallArgumentNormalizerChatClient(
                openAiClient.GetChatClient(options.DashScopeModel).AsIChatClient())
            .AsAIAgent(
                new ChatClientAgentOptions
                {
                    Name = "SchoolResearchAnalyst",
                    Description = "基于本地 skills 采集微信公众号与小红书公开内容，分析并输出学校评分 JSON。",
                    ChatOptions = new ChatOptions
                    {
                        Instructions = SystemInstructions,
                        Tools = [searchWechatTool, searchXiaohongshuTool, new HostedWebSearchTool()],
                    },
                    //AIContextProviders = [skillsProvider],
                },
                loggerFactory: loggerFactory);

        _logger = logger;
    }

    // ─── 公共入口 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 分析单所学校的公开舆情内容，返回结构化评分结果。
    /// Agent 自主决定调用哪些 skill、何时完成调研，最终输出 JSON。
    /// </summary>
    public async Task<SchoolAnalysis> AnalyzeAsync(School school, CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(school);

        try
        {
            var response = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);
            var raw = response.Text;

            _logger.LogInformation("Agent 分析原始返回: {SchoolName} => {RawResponse}", school.Name, raw);

            var json = ExtractJson(raw)
                ?? throw new InvalidOperationException("Agent 未返回可解析的 JSON");

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

    private static string BuildPrompt(School school) => $$"""
请调研并分析下面这所学校的公开内容，给出保守且可解释的择校评分分析。

【学校基本信息】
名称：{{school.Name}}
地址：{{school.Address}}
类型：{{SchoolTypes.DisplayName(school.Type)}}
距用户位置：{{school.Distance}} 米

调研要求（必须先搜索再分析）：
1. 调用 wechat-article-search skill搜索微信公众号文章，参数必须按 AIFunctionArguments 形式传入：new AIFunctionArguments { ["query"] = "{{school.Name}}" }。
2. 调用 xiaohongshu-search skill搜索小红书内容，参数必须按 AIFunctionArguments 形式传入：new AIFunctionArguments { ["query"] = "{{school.Name}}" }。
3. 若上述工具失败或无结果，可使用联网搜索工具补充。
4. 两个平台都要尝试搜索"{{school.Name}}"；若结果稀少，在分数和总结中体现保守判断。

调用 skill 参数格式要求：
- 必须传 AIFunctionArguments 风格的键值对参数对象，而不是 JSON 字符串、JSON 对象文本、数组、裸字符串
- 正确示例：new AIFunctionArguments { ["query"] = "{{school.Name}}" }
- 错误示例：{"query":"{{school.Name}}"}、["{{school.Name}}"]、"{{school.Name}}"

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
    { "platform": "微信公众号或小红书", "accountName": "可选", "postCount": 整数, "lastPostDate": "可选", "url": "来源地址" }
  ]
}
""";

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

    private const string SystemInstructions =
        """
        你是一个有着10年专业经验的择校分析师，可以根据学校的公开信息和舆情数据进行综合分析，提供专业且可解释的择校建议。
        工作流程：先调用tool搜索学校公开信息，再根据搜索结果输出分析 JSON。

        可用 tool：
        - search_wechat_articles(query, num?)：用于搜索微信公众号文章，query 必填，对应 wechat-article-search。
        - search_xiaohongshu_notes(keyword, limit?)：用于搜索小红书内容，keyword 必填，对应 xiaohongshu-search。
        - 联网搜索工具：补充使用。

        你必须实际调用 tool，不要只描述计划。

        强制执行步骤：
        第一步：调用 wechat-article-search，按 AIFunctionArguments 形式传参：new AIFunctionArguments { ["query"] = "学校名称" }。
        第二步：调用 xiaohongshu-search，按 AIFunctionArguments 形式传参：new AIFunctionArguments { ["query"] = "学校名称" }。
        第三步：若结果不足，可使用联网搜索工具补充。
        第四步：根据所有搜索结果输出严格 JSON 分析。

        调用 skill 时参数必须是 AIFunctionArguments 形式，例如 new AIFunctionArguments { ["query"] = "北京四中" }。
        不要传 JSON 字符串，不要传 JSON 对象文本，不要传数组，不要传裸字符串。
        正确：new AIFunctionArguments { ["query"] = "北京四中" }
        错误：{"query":"北京四中"}、["北京四中"]、"北京四中"

        禁止事项：
        - 禁止跳过工具调用直接输出结果，必须先搜索再分析。
        - 禁止凭空编造学校公众号、账号、活动、奖项、师资或口碑。
        - 如果所有搜索都无结果，在结论中体现样本不足，不要猜测。
        - 最终只返回严格 JSON，不要输出 Markdown 代码块，不要解释过程。
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}