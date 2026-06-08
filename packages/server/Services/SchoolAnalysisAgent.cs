using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Options;
using System.Reflection;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;

namespace SchoolRecommender.Api.Services;

/// <summary>
/// 学校公开内容分析 Agent。
/// 使用 DashScope/OpenAI 兼容接口，通过显式注册的工具函数（search_wechat_articles、
/// search_school_info）从多个渠道采集学校公开信息，再输出结构化评分 JSON。
/// 内置 DashScope 响应兼容层（<see cref="DashScopeResponseFixerHandler"/>、
/// <see cref="FunctionCallArgumentNormalizerChatClient"/>），修复参数格式问题。
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

        // DashScopeResponseFixerHandler：在 OpenAI SDK 反序列化前，修正 DashScope 把
        // tool_call arguments 序列化为数组而非对象的格式问题。
        var fixerHandler = new DashScopeResponseFixerHandler();
        var httpClient = new HttpClient(fixerHandler);

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(options.DashScopeApiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(options.DashScopeBaseUrl, UriKind.Absolute),
                Transport = new HttpClientPipelineTransport(httpClient),
            });

        // 显式注册带完整参数 schema 的工具，让模型可靠地传入 query/keyword 等参数。
        // AgentSkillsProvider 自动生成的 run_skill_script 不带 schema，部分模型无法可靠传参，故不使用。
        var searchWechatTool = AIFunctionFactory.Create(
            (string query, int? num, string? region, CancellationToken ct)
                => skillScriptRunner.SearchWechatArticlesAsync(query, num, region, ct),
            name: "search_wechat_articles",
            description: "搜索微信公众号文章。query 为必填关键词（学校名称），num 为返回条数（默认 10），region 为学校完整地址（工具自动提取市+区消歧）。返回含标题、链接、摘要、发布时间、来源公众号的 JSON。");

        var searchSchoolInfoTool = AIFunctionFactory.Create(
            (string school_name, string? aspect, string? region, CancellationToken ct)
                => skillScriptRunner.SearchSchoolInfoAsync(school_name, aspect, region, ct),
            name: "search_school_info",
            description: "学校信息专用联网搜索：自动清洗校名、追加维度关键词、过滤无关结果。\n- school_name（必填）：学校全名（带括号也可，工具会自动清洗）。\n- aspect（可选）：搜索维度，可选值：师资 / 评价 / 招生 / 活动 / 新闻；每次调用传一个维度，结果差异化更好。\n- region（可选）：学校完整地址（如 \"重庆市巴南区鱼轻路10号\"），工具自动提取市+区前缀消歧。\n返回 { matched_tokens, results:[{title,url,snippet}] }，无相关结果时返回 note 说明。");

        var chatClient = CreateChatClient(openAiClient, options);

        // FunctionCallArgumentNormalizerChatClient：在 Microsoft.Extensions.AI 层
        // 修复 DashScope/Qwen 将函数参数以数组而非对象形式传递的兼容性问题。
        var agentChatOptions = new ChatOptions
        {
            Instructions = SystemInstructions,
            Tools = [searchWechatTool, searchSchoolInfoTool],
        };

        _agent = new FunctionCallArgumentNormalizerChatClient(chatClient)
            .AsAIAgent(
                new ChatClientAgentOptions
                {
                    Name = "SchoolResearchAnalyst",
                    Description = "采集微信公众号与联网公开内容，分析学校并输出结构化评分 JSON。",
                    ChatOptions = agentChatOptions,
                },
                loggerFactory: loggerFactory);

        _logger = logger;
    }

    private static IChatClient CreateChatClient(OpenAIClient openAiClient, SchoolServiceOptions options)
    {
        if (!options.DashScopeUseResponsesApi)
        {
            return openAiClient.GetChatClient(options.DashScopeModel).AsIChatClient();
        }

        var responsesClient = typeof(OpenAIClient)
            .GetMethod("GetResponsesClient", BindingFlags.Public | BindingFlags.Instance, binder: null, types: Type.EmptyTypes, modifiers: null)
            ?.Invoke(openAiClient, null)
            ?? throw new InvalidOperationException("当前 OpenAI SDK 不支持 ResponsesClient，请升级 OpenAI 包版本。");

        var extensionsAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Microsoft.Extensions.AI.OpenAI", StringComparison.Ordinal))
            ?? Assembly.Load("Microsoft.Extensions.AI.OpenAI");

        var openAiExtensionsType = extensionsAssembly.GetType("Microsoft.Extensions.AI.OpenAIClientExtensions")
            ?? throw new InvalidOperationException("未找到 Microsoft.Extensions.AI.OpenAI 的 ResponsesClient 适配扩展。");

        var asIChatClientMethod = openAiExtensionsType.GetMethod(
            "AsIChatClient",
            BindingFlags.Public | BindingFlags.Static,
            [responsesClient.GetType(), typeof(string)]);

        if (asIChatClientMethod?.Invoke(null, [responsesClient, options.DashScopeModel]) is IChatClient chatClient)
        {
            return chatClient;
        }

        throw new InvalidOperationException("无法将 ResponsesClient 适配为 IChatClient，请检查 Microsoft.Extensions.AI.OpenAI 包版本。");
    }

    // ─── 公共入口 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 分析单所学校的公开舆情内容，返回结构化评分结果。
    /// Agent 自主决定调用哪些工具、何时完成调研，最终输出 JSON。
    /// </summary>
    public async Task<SchoolAnalysis> AnalyzeAsync(School school, int? studentScore, CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(school, studentScore);

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
// 第二步：调用 search_xiaohongshu_notes，keyword="{{school.Name}}"，region="{{school.CityName}}{{school.AdName}}"，搜索小红书内容。
    // ─── 提示构建 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 构造发送给 Agent 的用户提示。系统行为规则由 <see cref="SystemInstructions"/> 统一定义，
    /// 用户提示仅提供学校具体信息及各工具调用所需的精确参数。
    /// </summary>
    private static string BuildPrompt(School school, int? studentScore)
    {
        var isSeniorHigh = string.Equals(school.Type, SchoolTypes.SeniorHigh, StringComparison.OrdinalIgnoreCase);

        var seniorHighSection = isSeniorHigh
            ? $$"""

【高中专项：录取分数线调研】
此学校为高中，请额外执行以下步骤：
- 步骤 5：调用 search_school_info，school_name="{{school.Name}}", aspect="招生"，搜索该校近 5 年录取分数线。
- 步骤 6：调用 search_school_info，school_name="{{school.Name}} 录取分数线"，搜索补充数据。
- 综合整理近 5 年（2021-2025）的中考/高考录取分数线，输出到 admissionCutoffScores 字段。
{{(studentScore.HasValue ? $"- 学生当前分数：{studentScore.Value} 分，请根据历年录取线评估录取可能性。" : "- 未提供学生分数，仅整理录取线数据。")}}

admissionCutoffScores 输出格式（JSON 字符串）：
[{{string.Concat("{")}}"year":2021,"score":xxx{{string.Concat("}")}},{{string.Join(",", Enumerable.Range(2022, 4).Select(y => $"{{\"year\":{y},\"score\":xxx}}"))}}]
如果某年数据缺失，score 设为 0。如果完全搜不到数据，admissionCutoffScores 设为 null。
"""
            : "";

        return $$"""
请按照系统指令中的调研步骤，分析以下学校并输出 JSON。

【学校信息】
名称：{{school.Name}}
地址：{{school.CityName}}{{school.AdName}}
类型：{{SchoolTypes.DisplayName(school.Type)}}
距用户：{{school.Distance}} 米

【各工具调用参数参考】
- search_wechat_articles：query="{{school.Name}}", region="{{school.CityName}}{{school.AdName}}"
- search_school_info（评价）：school_name="{{school.Name}}", aspect="评价", region="{{school.CityName}}{{school.AdName}}"
- search_school_info（师资）：school_name="{{school.Name}}", aspect="师资", region="{{school.CityName}}{{school.AdName}}"
{{seniorHighSection}}
提示：search_wechat_articles 与 search_school_info 可并行调用以节省时间；两次 search_school_info 需顺序执行（aspect 不同）。

只返回如下格式的严格 JSON（不含 Markdown 代码块，不含解释文字）：
{
  "activityScore": 整数(0-100),
  "lastPostDate": "YYYY-MM-DD",
  "reputationScore": 整数(0-100),
  "reputationSummary": "一句话总结",
  "contentQualityScore": 整数(0-100),
  "teachingStaffScore": 整数(0-100),
  "teachingStaffDesc": "一句话",
  "awardsCount": 整数,
  "awardsDetail": ["字符串"],
  "studentActivityScore": 整数(0-100),
  "studentActivityDesc": "一句话",
  "sources": [{ "platform": "微信公众号|互联网", "accountName": "可选", "postCount": 整数, "lastPostDate": "可选", "url": "来源地址" }]{{(isSeniorHigh ? ",\n  \"admissionCutoffScores\": \"[{\\\"year\\\":2021,\\\"score\\\":xxx},...] 或 null\"" : "")}}
}
""";
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
        Sources: [],
        AdmissionCutoffScores: null);

    /// <summary>
    /// 系统级行为规则：定义 Agent 身份、可用工具说明、强制调研步骤及输出约束。
    /// 用户提示（BuildPrompt）只提供学校具体参数，避免重复描述规则。
    /// </summary>
    private const string SystemInstructions =
        """
        你是一位拥有10年经验的专业择校分析师，根据学校公开信息与舆情数据进行客观综合分析，给出保守且可解释的评分。

        【可用工具】
        - search_wechat_articles(query, num?, region?)：搜索微信公众号文章。query 必填（学校名称），region 建议传入学校完整地址（工具自动提取市+区消歧）。
        - search_school_info(school_name, aspect?, region?)：学校专用联网搜索，已做查询增强与相关性过滤。school_name 必填（带括号自动清洗），aspect 建议按维度分次调用（评价/师资/招生/活动/新闻），region 建议传学校完整地址。

        【强制调研流程】
        以下步骤全部必须执行，某步返回"未检索到相关结果"时也必须继续：
        步骤 1：调用 search_wechat_articles（获取微信公众号内容）
        步骤 2：调用 search_school_info，aspect="评价"（获取家长口碑）
        步骤 3：调用 search_school_info，aspect="师资"（获取师资信息）
        步骤 4：汇总全部结果，综合分析后输出 JSON
        注意：步骤 1 与步骤 2/3 可并行执行；步骤 2 与步骤 3 需依次执行。

        【高中专项】
        当用户提示中包含"高中专项：录取分数线调研"时，你必须额外执行：
        步骤 5：调用 search_school_info，aspect="招生"（搜索录取分数线）
        步骤 6：补充搜索该校录取分数线数据
        综合整理近5年录取分数线，填入 admissionCutoffScores 字段。如果提供了学生分数，需评估录取可能性。

        【禁止事项】
        - 不得跳过任何步骤，不得以"结果已足够"为由提前输出 JSON
        - 不得编造学校公众号、活动、奖项、师资或家长评价
        - 所有工具调用必须传入 region 参数
        - search_school_info 返回 note 字段时，结论中须体现样本不足；不得将过滤掉的内容作为依据
        - 样本稀少时评分应保守（30-50 分区间），不得给虚高分
        - 最终只输出严格 JSON，不加 Markdown 代码块或过程说明
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}