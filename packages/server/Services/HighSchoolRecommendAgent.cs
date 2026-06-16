using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Reflection;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Options;
using OpenAI.Responses;
using Microsoft.AspNetCore;

namespace SchoolRecommender.Api.Services;

/// <summary>
/// 高中择校推荐 Agent。
/// 根据学生摸底考试分数和所在省份，通过联网搜索获取该省份重点高中的
/// 近 5 年录取分数线，分析并按"冲/稳/保"三档推荐适合报考的高中。
/// </summary>
public sealed class HighSchoolRecommendAgent
{
    private readonly ChatClientAgent _agent;
    private readonly ILogger<HighSchoolRecommendAgent> _logger;

    public HighSchoolRecommendAgent(
        SchoolServiceOptions options,
        LocalSkillScriptRunner skillScriptRunner,
        ILoggerFactory loggerFactory,
        ILogger<HighSchoolRecommendAgent> logger)
    {
        if (string.IsNullOrWhiteSpace(options.DashScopeApiKey))
            throw new InvalidOperationException("未配置 DASHSCOPE_API_KEY 或 OPENAI_API_KEY，无法初始化高中择校推荐 Agent");

        var fixerHandler = new DashScopeResponseFixerHandler();
        var httpClient = new HttpClient(fixerHandler)
        {
            Timeout = options.DashScopeNetworkTimeout + TimeSpan.FromSeconds(10),
        };

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(options.DashScopeApiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(options.DashScopeBaseUrl, UriKind.Absolute),
                NetworkTimeout = options.DashScopeNetworkTimeout,
                Transport = new HttpClientPipelineTransport(httpClient),
            });

        // 注册联网搜索工具
        var searchSchoolInfoTool = AIFunctionFactory.Create(
            (string school_name, string? aspect, string? region, CancellationToken ct)
                => skillScriptRunner.SearchSchoolInfoAsync(school_name, aspect, region, ct),
            name: "search_school_info",
            description: "学校信息专用联网搜索：自动清洗校名、追加维度关键词、过滤无关结果。\n" +
                "- school_name（必填）：学校全名。\n" +
                "- aspect（可选）：搜索维度，可选值：录取分数线 / 评价 / 师资 / 招生 / 排名。\n" +
                "- region（可选）：学校所在城市或省份，用于消歧。\n" +
                "返回搜索结果列表。");

        var searchWebTool = AIFunctionFactory.Create(
            (string query, int? num, CancellationToken ct)
                => skillScriptRunner.SearchGeneralWebAsync(query, num, ct),
            name: "search_web",
            description: "通用联网搜索工具。query 为搜索关键词，返回搜索结果列表。" +
                "适用于搜索省份高中排名、录取分数线汇总等宏观信息。");

        var searchWechatArticlesTool = AIFunctionFactory.Create(
            (string query, int? num, string? region, CancellationToken ct)
                => skillScriptRunner.SearchWechatArticlesAsync(query, num, region, ct),
            name: "search_wechat_articles",
            description: "微信公众号文章搜索工具。\n" +
                "- query（必填）：搜索关键词，适合传入学校名、学校名+录取分数线、学校名+招生简章等。\n" +
                "- num（可选）：返回结果数。\n" +
                "- region（可选）：省份或城市，用于给关键词追加地域前缀，减少歧义。\n" +
                "适用于补充学校录取分数线、招生简章、家长经验、学校评价等微信公众号文章线索。返回文章列表。");

        // var fetchWebpageContentTool = AIFunctionFactory.Create(
        //     (string url, CancellationToken ct)
        //         => skillScriptRunner.FetchWebpageContentAsync(url, ct),
        //     name: "fetch_webpage_content",
        //     description: "读取公开网页正文内容。传入 search_school_info 或 search_web 返回结果中的 url，返回 { url, title, content, contentLength, admissionScoreHints }，用于从页面正文提取录取分数线、招生说明等细节。只支持公开 http/https 页面。");

        // 注册 AnySearch 联网搜索工具
        var searchAnyTool = AIFunctionFactory.Create(
            (string query, int? maxResults, string? domain, string? subDomain, string? subDomainParams, CancellationToken ct)
                => skillScriptRunner.SearchAnyAsync(query, maxResults, domain, subDomain, subDomainParams, ct),
            name: "search_any",
            description: "AnySearch 联网搜索工具，支持通用搜索和垂直领域搜索。\n" +
                "- query（必填）：搜索关键词。\n" +
                "- maxResults（可选）：返回结果数，1-10，默认 10。\n" +
                "- domain（可选）：垂直领域，如 finance / academic / health / legal / code 等。\n" +
                "- subDomain（可选）：子域标识，需先调用 get_sub_domains 获取。\n" +
                "- subDomainParams（可选）：子域参数，key=value 格式或 JSON。\n" +
                "适用于搜索学校官网信息、教育政策、录取分数线汇总等。返回搜索结果列表。");

        var batchSearchAnyTool = AIFunctionFactory.Create(
            (AnySearchBatchQuery[] queries, string? domain, string? subDomain, string? subDomainParams, CancellationToken ct)
                => skillScriptRunner.BatchSearchAnyAsync(queries, domain, subDomain, subDomainParams, ct),
            name: "batch_search_any",
            description: "AnySearch 批量搜索工具，支持多查询并行执行和混合领域搜索。\n" +
                "- queries（必填）：对象数组，例如 [{\"query\":\"重庆南开中学\",\"max_results\":5},{\"query\":\"重庆鱼洞云水苑幼儿园\",\"max_results\":5}]。\n" +
                "- domain / subDomain / subDomainParams（可选）：垂直领域参数。\n" +
                "适用于同时搜索多所学校的录取分数线、同时对比多所学校信息等场景。返回搜索结果列表。");

        // var extractWebpageAnyTool = AIFunctionFactory.Create(
        //     (string url, CancellationToken ct)
        //         => skillScriptRunner.ExtractWebpageAnyAsync(url, ct),
        //     name: "extract_webpage_any",
        //     description: "AnySearch 网页正文提取工具，返回 Markdown 格式。\n" +
        //         "- url（必填）：要提取的网页链接。\n" +
        //         "适用于从搜索结果链接中提取学校官网、教育局公告、招生简章等页面的正文内容。返回 Markdown 格式的正文。");

        var fetchWebpageContentTool = AIFunctionFactory.Create(
            (string url, CancellationToken ct)
                => skillScriptRunner.FetchWebpageContentAsync(url, ct),
            name: "fetch_webpage_content",
            description: "读取公开网页正文内容。传入 batch_search_any 返回结果中的 url，返回 { url, title, content, contentLength, admissionScoreHints }，用于从页面正文提取录取分数线、招生说明等细节。只支持公开 http/https 页面。");


        var chatClient = CreateChatClient(openAiClient, options);

        var agentChatOptions = new ChatOptions
        {
            Instructions = SystemInstructions,
            
                // searchSchoolInfoTool, searchWebTool, searchWechatArticlesTool, fetchWebpageContentTool,
            Tools = [
                batchSearchAnyTool, fetchWebpageContentTool
            ],
        };

        _agent = new FunctionCallArgumentNormalizerChatClient(chatClient)
            .AsAIAgent(
                new ChatClientAgentOptions
                {
                    Name = "HighSchoolRecommendAnalyst",
                    Description = "分析省份高中录取分数线，按冲/稳/保三档推荐适合学生报考的高中。",
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
            ?? throw new InvalidOperationException("未加载 Microsoft.Extensions.AI.OpenAI 程序集");

        var adapterType = extensionsAssembly.GetType("Microsoft.Extensions.AI.OpenAIResponseClientExtensions")
            ?? throw new InvalidOperationException("未找到 OpenAIResponseClientExtensions");

        var asIChatClientMethod = adapterType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "AsIChatClient" && m.GetParameters().Length == 1)
            ?? throw new InvalidOperationException("未找到 AsIChatClient 方法");

        return (IChatClient)asIChatClientMethod.Invoke(null, [responsesClient])!;
    }

    /// <summary>
    /// 根据学生分数和所在省份，推荐适合报考的高中（冲/稳/保）。
    /// </summary>
    public async Task<IReadOnlyList<HighSchoolRecommendation>> RecommendAsync(
        int studentScore,
        string province,
        CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(studentScore, province);

        try
        {
            var response = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);
            var text = response.Text ?? string.Empty;
            // _logger.LogDebug("高中择校推荐 Agent 原始输出: {Text}", text);
            return ParseRecommendations(text, province);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "高中择校推荐分析失败: Province={Province}, Score={Score}", province, studentScore);
            return Array.Empty<HighSchoolRecommendation>();
        }
    }


    private static string BuildPrompt(int studentScore, string province)
    {
        return $$"""
请根据以下信息，推荐适合学生报考的高中学校：

- 学生摸底考试分数：{{studentScore}} 分
- 所在省份：{{province}}

═══════════════════════════════════════════
【第一步：批量搜索获取高中名单（必须覆盖不同分数段）】
═══════════════════════════════════════════

1. 调用 batch_search_any(queries=[{"query":"{{province}} 高中排名","max_results":10},{"query":"{{province}} 省级示范高中","max_results":10},{"query":"{{province}} 重点高中名单","max_results":10},{"query":"{{province}} 中考录取分数线 2025 2024 2023","max_results":10}]) 一次性获取多维度搜索结果
2. 必须选出至少 15-20 所高中，且必须覆盖以下三类分数段：
   - 高分段学校（录取线明显高于学生分数）→ 后续归为"冲"
   - 中分段学校（录取线与学生分数接近）→ 后续归为"稳"
   - 低分段学校（录取线明显低于学生分数）→ 后续归为"保"
3. 如果只搜到录取线低的学校，必须继续搜索其他城市/区县的高分段学校，确保三档都有足够候选

═══════════════════════════════════════════
【第二步：逐一搜索每所高中的详细信息】
═══════════════════════════════════════════

将所有候选学校一次性构建为批量查询数组，调用 batch_search_any 搜索：
a) queries 数组中每所学校包含两个查询：{"query":"XX中学 中考录取分数线 2025 2024 2023 2022","max_results":10} 和 {"query":"XX中学 学校评价 特色 师资","max_results":10}
b) 一次 batch_search_any 调用即可覆盖全部学校的录取分数线和评价信息
c) 对 batch_search_any 返回结果中 url 最相关的条目，调用 fetch_webpage_content(url="...") 读取正文，再从正文中提取年份与分数；不能只依赖搜索摘要

═══════════════════════════════════════════
【第三步：分类标准（严格执行）】
═══════════════════════════════════════════

- "冲"：学生分数 < 该校平均录取线，差距 5~40 分。至少选 3 所。
- "稳"：学生分数 ≈ 该校平均录取线（差距 -15~+15 分）。至少选 3 所。
- "保"：学生分数 > 该校平均录取线 + 15 分。至少选 3 所。

★ 关键：如果某个分类候选不足，扩大搜索范围（搜索更多城市、更多学校），直到每个分类至少有 3 所。
★ 总共推荐 9-15 所学校。

═══════════════════════════════════════════
【输出格式】
═══════════════════════════════════════════
你必须只返回严格 JSON 数组本身。
禁止返回以下任何额外内容：
- 禁止返回前置文字
- 禁止返回 Markdown 代码块，如 ```json
- 禁止返回数组前后的任何自然语言、注释、总结、提醒、空行包装
- 输出的第一个字符必须是 `[`
- 输出的最后一个字符必须是 `]`

如果你准备输出的内容不是一个可直接被 JSON.parse 成功解析的 JSON 数组，则必须立即重新组织，直到满足要求为止。

每所学校必须包含以下所有字段（搜不到的字段填 null）：
[
  {
    "schoolName": "XX中学",
    "city": "所在城市",
    "admissionScores": [
      {"year": 2025, "score": 680},
      {"year": 2024, "score": 675},
      {"year": 2023, "score": 670},
      {"year": 2022, "score": 665},
      {"year": 2021, "score": 660}
    ],
    "category": "冲",
    "matchScore": 35,
    "avgAdmissionScore": 675,
    "recommendation": "详细推荐理由，50-100字，包含学校优势和报考建议，内容中有双引号的需要反斜杠转译",
    "schoolLevel": "省级示范高中",
    "schoolNature": "公办",
    "schoolFeatures": "理科见长，竞赛成绩突出",
    "teachingStaffInfo": "特级教师5人，高级教师占比60%",
    "keyDisciplines": "数学、物理、化学",
    "studentCount": 3000,
    "address": "XX市XX区XX路XX号",
    "website": "https://www.xxx.cn",
    "enrollmentPlan": 800,
    "enrollmentNote": "面向全市招生，提供住宿",
    "minAdmissionScore": 660,
    "maxAdmissionScore": 680
  }
]

【注意】
- 只能输出指定 json格式的数据，不要额外增加任何前置说明或后置说明
- avgAdmissionScore = 近年有效分数的平均值（四舍五入取整）
- matchScore = 匹配分（0-100），100=远超录取线，50=持平，0=远低于
- minAdmissionScore / maxAdmissionScore = 近年录取线的最低/最高值
- recommendation 中的内容最好不要携带双引号,如果确实有双引号，需要反斜杠转译 例：重庆\"七龙珠 \"
- 如果某年数据缺失，score 填 0
- 优先依据网页正文中的明确年份+分数信息填写 admissionScores；搜索摘要只能作为线索，不能直接当最终分数证据
- 若 fetch_webpage_content 返回 admissionScoreHints，优先使用其中的年份+分数线索，再回到正文上下文核对是否确实是该校录取线
- 仅当“年份”和“分数”在同一段正文附近同时出现，并伴随“录取分数线 / 统招线 / 投档线 / 最低录取 / 中考”等词时，才可记入 admissionScores
- 同一页面若出现多个分数，优先选择与学校名、年份、录取线关键词同时邻近的分数；明显属于学费、满分、班型分班、排名数字的内容不得算作录取线
- 如果无法确认某一年分数是否为该校正式录取线，该年份 score 必须填 0，不得猜测
- 不同省份中考总分不同，请根据省份实际分析
- 禁止编造数据，搜不到就跳过该校
""";
    }


//     private static string BuildPrompt(int studentScore, string province)
//     {
//         return $$"""
// 请根据以下信息，推荐适合学生报考的高中学校：

// - 学生摸底考试分数：{{studentScore}} 分
// - 所在省份：{{province}}

// ═══════════════════════════════════════════
// 【第一步：获取高中名单（必须覆盖不同分数段）】
// ═══════════════════════════════════════════

// 1. 搜索"{{province}} 高中排名""{{province}} 省级示范高中""{{province}} 重点高中名单"
// 2. 必须选出至少 15-20 所高中，且必须覆盖以下三类分数段：
//    - 高分段学校（录取线明显高于学生分数）→ 后续归为"冲"
//    - 中分段学校（录取线与学生分数接近）→ 后续归为"稳"
//    - 低分段学校（录取线明显低于学生分数）→ 后续归为"保"
// 3. 如果只搜到录取线低的学校，必须继续搜索其他城市/区县的高分段学校，确保三档都有足够候选

// ═══════════════════════════════════════════
// 【第二步：逐一搜索每所高中的详细信息】
// ═══════════════════════════════════════════

// 对每所高中，分别搜索以下信息：
// a) 录取分数线：search_school_info(school_name="XX中学", aspect="录取分数线", region="{{province}}")
// b) 学校评价与特色：search_school_info(school_name="XX中学", aspect="评价", region="{{province}}")
// c) 如仍缺数据，用 search_web(query="{{province}} XX中学 中考录取分数线 2025 2024 2023")
// d) 若 search_web / search_school_info 结果不足，必须补充调用 search_wechat_articles(query="XX中学", region="{{province}}") 获取公众号文章线索
// e) 对包含录取分数或招生简章线索的结果，必须继续调用 fetch_webpage_content(url="...") 读取正文，再从正文中提取年份与分数；不能只依赖搜索摘要

// ═══════════════════════════════════════════
// 【第三步：分类标准（严格执行）】
// ═══════════════════════════════════════════

// - "冲"：学生分数 < 该校平均录取线，差距 5~40 分。至少选 3 所。
// - "稳"：学生分数 ≈ 该校平均录取线（差距 -15~+15 分）。至少选 3 所。
// - "保"：学生分数 > 该校平均录取线 + 15 分。至少选 3 所。

// ★ 关键：如果某个分类候选不足，扩大搜索范围（搜索更多城市、更多学校），直到每个分类至少有 3 所。
// ★ 总共推荐 9-15 所学校。

// ═══════════════════════════════════════════
// 【输出格式】
// ═══════════════════════════════════════════

// 你必须只返回严格 JSON 数组本身。
// 禁止返回以下任何额外内容：
// - 禁止返回“以下是结果”“说明”“分析如下”等前置文字
// - 禁止返回 Markdown 代码块，如 ```json
// - 禁止返回数组前后的任何自然语言、注释、总结、提醒、空行包装
// - 输出的第一个字符必须是 `[`
// - 输出的最后一个字符必须是 `]`

// 如果你准备输出的内容不是一个可直接被 JSON.parse 成功解析的 JSON 数组，则必须立即重新组织，直到满足要求为止。

// 每所学校必须包含以下所有字段（搜不到的字段填 null）：

// [
//   {
//     "schoolName": "XX中学",
//     "city": "所在城市",
//     "admissionScores": [
//       {"year": 2025, "score": 680},
//       {"year": 2024, "score": 675},
//       {"year": 2023, "score": 670},
//       {"year": 2022, "score": 665},
//       {"year": 2021, "score": 660}
//     ],
//     "category": "冲",
//     "matchScore": 35,
//     "avgAdmissionScore": 675,
//     "recommendation": "详细推荐理由，50-100字，包含学校优势和报考建议",
//     "schoolLevel": "省级示范高中",
//     "schoolNature": "公办",
//     "schoolFeatures": "理科见长，竞赛成绩突出",
//     "teachingStaffInfo": "特级教师5人，高级教师占比60%",
//     "keyDisciplines": "数学、物理、化学",
//     "studentCount": 3000,
//     "address": "XX市XX区XX路XX号",
//     "website": "https://www.xxx.cn",
//     "enrollmentPlan": 800,
//     "enrollmentNote": "面向全市招生，提供住宿",
//     "minAdmissionScore": 660,
//     "maxAdmissionScore": 680
//   }
// ]

// 【注意】
// - avgAdmissionScore = 近年有效分数的平均值（四舍五入取整）
// - matchScore = 匹配分（0-100），100=远超录取线，50=持平，0=远低于
// - minAdmissionScore / maxAdmissionScore = 近年录取线的最低/最高值
// - 如果某年数据缺失，score 填 0
// - 优先依据网页正文中的明确年份+分数信息填写 admissionScores；搜索摘要只能作为线索，不能直接当最终分数证据
// - 若 fetch_webpage_content 返回 admissionScoreHints，优先使用其中的年份+分数线索，再回到正文上下文核对是否确实是该校录取线
// - 仅当“年份”和“分数”在同一段正文附近同时出现，并伴随“录取分数线 / 统招线 / 投档线 / 最低录取 / 中考”等词时，才可记入 admissionScores
// - 同一页面若出现多个分数，优先选择与学校名、年份、录取线关键词同时邻近的分数；明显属于学费、满分、班型分班、排名数字的内容不得算作录取线
// - 如果无法确认某一年分数是否为该校正式录取线，该年份 score 必须填 0，不得猜测
// - 不同省份中考总分不同，请根据省份实际分析
// - 禁止编造数据，搜不到就跳过该校
// """;
//     }

    private IReadOnlyList<HighSchoolRecommendation> ParseRecommendations(string rawText, string province)
    {
        try
        {
            var json = ExtractJson(rawText);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("未能从 Agent 输出中提取 JSON，省份: {Province}", province);
                return Array.Empty<HighSchoolRecommendation>();
            }

            var normalizedJson = NormalizeRecommendationJson(json);
            var recommendations = JsonSerializer.Deserialize<List<HighSchoolRecommendation>>(normalizedJson, JsonOptions);
            if (recommendations is null || recommendations.Count == 0)
            {
                _logger.LogWarning("Agent 返回了空的推荐列表，省份: {Province}", province);
                return Array.Empty<HighSchoolRecommendation>();
            }

            // 验证分类完整性
            var hasReach = recommendations.Any(r => r.Category == "冲");
            var hasMatch = recommendations.Any(r => r.Category == "稳");
            var hasSafety = recommendations.Any(r => r.Category == "保");

            if (!hasReach || !hasMatch || !hasSafety)
            {
                _logger.LogWarning("Agent 返回的推荐缺少某些分类: 冲={HasReach}, 稳={HasMatch}, 保={HasSafety}",
                    hasReach, hasMatch, hasSafety);
            }

            return recommendations;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON 反序列化失败: {Province}", province);
            return Array.Empty<HighSchoolRecommendation>();
        }
    }

    private static string? ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // 尝试提取 Markdown 代码块中的 JSON
        var codeBlockStart = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (codeBlockStart < 0) codeBlockStart = text.IndexOf("```");
        if (codeBlockStart >= 0)
        {
            var contentStart = text.IndexOf('\n', codeBlockStart);
            if (contentStart >= 0)
            {
                var codeBlockEnd = text.IndexOf("```", contentStart + 1);
                if (codeBlockEnd >= 0)
                {
                    return text[(contentStart + 1)..codeBlockEnd].Trim();
                }
            }
        }

        // 尝试直接提取最外层 JSON 数组，自动忽略前置说明文字
        var arrayJson = ExtractBalancedJsonBlock(text, '[', ']');
        if (!string.IsNullOrWhiteSpace(arrayJson))
        {
            return arrayJson;
        }

        // 尝试提取最外层 JSON 对象
        var objectJson = ExtractBalancedJsonBlock(text, '{', '}');
        if (!string.IsNullOrWhiteSpace(objectJson))
        {
            return objectJson;
        }

        return null;
    }

    private static string? ExtractBalancedJsonBlock(string text, char openChar, char closeChar)
    {
        var start = text.IndexOf(openChar);
        while (start >= 0)
        {
            var depth = 0;
            var inString = false;
            var isEscaped = false;

            for (var i = start; i < text.Length; i++)
            {
                var current = text[i];

                if (inString)
                {
                    if (isEscaped)
                    {
                        isEscaped = false;
                        continue;
                    }

                    if (current == '\\')
                    {
                        isEscaped = true;
                        continue;
                    }

                    if (current == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    continue;
                }

                if (current == openChar)
                {
                    depth++;
                    continue;
                }

                if (current == closeChar)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text[start..(i + 1)].Trim();
                    }
                }
            }

            start = text.IndexOf(openChar, start + 1);
        }

        return null;
    }

    private static string NormalizeRecommendationJson(string json)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return json;
        }

        if (root is not JsonArray recommendations)
        {
            return json;
        }

        foreach (var recommendationNode in recommendations.OfType<JsonObject>())
        {
            NormalizeIntField(recommendationNode, "matchScore");
            NormalizeIntField(recommendationNode, "avgAdmissionScore");
            NormalizeDoubleField(recommendationNode, "minAdmissionScore");
            NormalizeDoubleField(recommendationNode, "maxAdmissionScore");
            NormalizeStringField(recommendationNode, "studentCount");
            NormalizeStringField(recommendationNode, "enrollmentPlan");

            if (recommendationNode["admissionScores"] is JsonArray admissionScores)
            {
                foreach (var scoreNode in admissionScores.OfType<JsonObject>())
                {
                    NormalizeIntField(scoreNode, "year");
                    NormalizeDoubleField(scoreNode, "score");
                }
            }
        }

        return root.ToJsonString();
    }

    private static void NormalizeIntField(JsonObject node, string fieldName)
    {
        if (node[fieldName] is null)
        {
            return;
        }

        var value = ReadDoubleValue(node[fieldName]);
        if (value is not null)
        {
            node[fieldName] = (int)Math.Round(value.Value);
        }
    }

    private static void NormalizeDoubleField(JsonObject node, string fieldName)
    {
        if (node[fieldName] is null)
        {
            return;
        }

        var value = ReadDoubleValue(node[fieldName]);
        if (value is not null)
        {
            node[fieldName] = value.Value;
        }
    }

    private static void NormalizeStringField(JsonObject node, string fieldName)
    {
        if (node[fieldName] is null)
        {
            return;
        }

        if (node[fieldName] is JsonValue valueNode)
        {
            if (valueNode.TryGetValue<string>(out var textValue))
            {
                node[fieldName] = textValue;
                return;
            }

            if (valueNode.TryGetValue<double>(out var numericValue))
            {
                node[fieldName] = Math.Round(numericValue).ToString("0");
            }
        }
    }

    private static double? ReadDoubleValue(JsonNode? node)
    {
        if (node is not JsonValue valueNode)
        {
            return null;
        }

        if (valueNode.TryGetValue<double>(out var numericValue))
        {
            return numericValue;
        }

        if (valueNode.TryGetValue<string>(out var textValue)
            && double.TryParse(textValue, out var parsedValue))
        {
            return parsedValue;
        }

        return null;
    }

    private const string SystemInstructions =
        """
        你是一位资深教育择校顾问，拥有丰富的中考志愿填报经验。你的任务是根据学生的摸底考试成绩和所在省份，
        通过联网搜索获取该省份各重点高中的历年录取分数线和详细办学信息，然后按"冲一冲、稳一稳、保一保"三档
        为学生推荐最适合报考的高中学校。

        【可用工具】
                - batch_search_any(queries, domain?, subDomain?, subDomainParams?)：AnySearch 批量搜索工具，支持多查询并行执行和混合领域搜索。
          - queries（必填）：[{"query":"XX省 高中排名","max_results":10},{"query":"XX省 省级示范高中","max_results":10},{"query":"XX省 重点高中名单","max_results":10},{"query":"XX省 中考录取分数线 2025 2024 2023","max_results":10}]，支持同时搜索多所学校或多个维度。
          - 适用于同时搜索省份高中排名、多所学校录取分数线、学校评价等。
          - 返回搜索结果列表。
        - fetch_webpage_content(url)：读取公开网页正文。必须用于打开搜索结果中的候选链接，提取录取分数线原文证据。

        【强制调研流程】
        步骤 1：调用 batch_search_any，传入省份高中排名相关多个查询（参数： [{"query":"XX省 高中排名","max_results":10},{"query":"XX省 中考录取分数线 2025 2024 2023 2022","max_results":10},{"query":"XX省 省级示范高中","max_results":10},{"query":"XX省 重点高中名单","max_results":10}]），获取 15-20 所代表性高中。
               ⚠ 必须覆盖不同分数段的学校（高分段、中分段、低分段），不能只搜低分学校。
        步骤 2：将步骤1获得的学校列表，构建批量查询数组（每所学校两个查询：{"query":"XX中学 中考录取分数线 2025 2024 2023 2022","max_results":10} 和 {"query":"XX中学 学校评价 特色 师资","max_results":10}），一次调用 batch_search_any 搜索所有学校的信息。
        步骤 3：对 batch_search_any 返回的候选结果，挑选最相关的 url，继续调用 fetch_webpage_content 读取正文，并从正文中提取带年份的录取分数线。
               ⚠ 不允许只根据标题或摘要直接填写分数；若正文无明确分数，则该年份记为 0 或跳过该校。
               ⚠ 若 fetch_webpage_content 返回了 admissionScoreHints，先用它定位候选年份与分数，再回看正文确认该数字是否真的是该校录取线。
        步骤 4：将学校严格按以下标准分为三档，每档至少 3 所：
               - "冲"：学生分数 < 平均录取线，差距 5~40 分
               - "稳"：学生分数 ≈ 平均录取线（差距 -15~+15 分）
               - "保"：学生分数 > 平均录取线 + 15 分
               如果某档候选不足，必须扩大搜索范围补足。
        步骤 5：只输出完整信息的 JSON 数组，不要额外增加前置说明或后置说明文字。

        【注意事项】
        - 不得编造录取分数线数据
         - 不得把搜索结果摘要中的模糊数字直接当成录取线，必须优先核对网页正文
        - 不得把学费、总分、实验班分班线、排名、上线率、招生人数当成录取线
        - 搜不到数据的学校应跳过，不得用猜测数据
        - 不同省份中考总分不同，必须根据省份实际情况分析
        - 最终回复必须只包含 JSON 数组本身，不允许有任何前缀、后缀、解释、标题、备注、代码块标记
        - 每个分类（冲/稳/保）至少 3 所学校，这是硬性要求
        """;

    // private const string SystemInstructions =
    //     """
    //     你是一位资深教育择校顾问，拥有丰富的中考志愿填报经验。你的任务是根据学生的摸底考试成绩和所在省份，
    //     通过联网搜索获取该省份各重点高中的历年录取分数线和详细办学信息，然后按"冲一冲、稳一稳、保一保"三档
    //     为学生推荐最适合报考的高中学校。

    //     【可用工具】
    //     - search_school_info(school_name, aspect?, region?)：学校专用联网搜索。
    //       school_name 必填。
    //       aspect 可选值：录取分数线 / 评价 / 师资 / 招生 / 排名。
    //       region 传入省份或城市用于消歧。
    //     - search_web(query, num?)：通用联网搜索。适用于搜索省份高中排名、录取分数线汇总等宏观信息。
    //             - search_wechat_articles(query, num?, region?)：微信公众号文章搜索。适用于补充学校录取分数线、招生简章、学校评价、家长经验等公众号内容线索。
    //             - fetch_webpage_content(url)：读取公开网页正文。必须用于打开搜索结果中的候选链接，提取录取分数线原文证据。

    //     【强制调研流程】
    //     步骤 1：调用 search_web，搜索省份高中排名和省级示范高中名单，获取 15-20 所代表性高中。
    //            ⚠ 必须覆盖不同分数段的学校（高分段、中分段、低分段），不能只搜低分学校。
    //     步骤 2：对每所高中，分别调用：
    //            - search_school_info(aspect="录取分数线")：获取近 5 年录取线
    //            - search_school_info(aspect="评价")：获取学校特色、师资等信息
    //            - 若网页搜索结果不足或偏少，继续调用 search_wechat_articles(query="学校名 录取分数线/招生简章/中考", region="省份") 补充线索
    //        步骤 3：对 search_school_info / search_web / search_wechat_articles 返回的候选结果，挑选最相关的 1-3 个 url，继续调用 fetch_webpage_content 读取正文，并从正文中提取带年份的录取分数线。
    //          ⚠ 不允许只根据标题或摘要直接填写分数；若正文无明确分数，则该年份记为 0 或跳过该校。
    //          ⚠ 若 fetch_webpage_content 返回了 admissionScoreHints，先用它定位候选年份与分数，再回看正文确认该数字是否真的是该校录取线。
    //      步骤 4：将学校严格按以下标准分为三档，每档至少 3 所：
    //            - "冲"：学生分数 < 平均录取线，差距 5~40 分
    //            - "稳"：学生分数 ≈ 平均录取线（差距 -15~+15 分）
    //            - "保"：学生分数 > 平均录取线 + 15 分
    //            如果某档候选不足，必须扩大搜索范围补足。
    //      步骤 5：输出包含完整信息的 JSON 数组。

    //     【禁止事项】
    //     - 不得编造录取分数线数据
    //      - 不得把搜索结果摘要中的模糊数字直接当成录取线，必须优先核对网页正文
    //     - 不得把学费、总分、实验班分班线、排名、上线率、招生人数当成录取线
    //     - 搜不到数据的学校应跳过，不得用猜测数据
    //     - 不同省份中考总分不同，必须根据省份实际情况分析
    //     - 最终回复必须只包含 JSON 数组本身，不允许有任何前缀、后缀、解释、标题、备注、代码块标记
    //     - 每个分类（冲/稳/保）至少 3 所学校，这是硬性要求
    //     """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
