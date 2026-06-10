using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Reflection;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Options;

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
        var httpClient = new HttpClient(fixerHandler);

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(options.DashScopeApiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(options.DashScopeBaseUrl, UriKind.Absolute),
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
                => skillScriptRunner.SearchSchoolInfoAsync(query, null, null, ct),
            name: "search_web",
            description: "通用联网搜索工具。query 为搜索关键词，返回搜索结果列表。" +
                "适用于搜索省份高中排名、录取分数线汇总等宏观信息。");

        var chatClient = CreateChatClient(openAiClient, options);

        var agentChatOptions = new ChatOptions
        {
            Instructions = SystemInstructions,
            Tools = [searchSchoolInfoTool, searchWebTool],
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
            _logger.LogDebug("高中择校推荐 Agent 原始输出: {Text}", text);
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
【第一步：获取高中名单（必须覆盖不同分数段）】
═══════════════════════════════════════════

1. 搜索"{{province}} 高中排名""{{province}} 省级示范高中""{{province}} 重点高中名单"
2. 必须选出至少 15-20 所高中，且必须覆盖以下三类分数段：
   - 高分段学校（录取线明显高于学生分数）→ 后续归为"冲"
   - 中分段学校（录取线与学生分数接近）→ 后续归为"稳"
   - 低分段学校（录取线明显低于学生分数）→ 后续归为"保"
3. 如果只搜到录取线低的学校，必须继续搜索其他城市/区县的高分段学校，确保三档都有足够候选

═══════════════════════════════════════════
【第二步：逐一搜索每所高中的详细信息】
═══════════════════════════════════════════

对每所高中，分别搜索以下信息：
a) 录取分数线：search_school_info(school_name="XX中学", aspect="录取分数线", region="{{province}}")
b) 学校评价与特色：search_school_info(school_name="XX中学", aspect="评价", region="{{province}}")
c) 如仍缺数据，用 search_web(query="{{province}} XX中学 中考录取分数线 2024 2023")

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

只返回严格 JSON 数组，不含 Markdown 代码块，不含解释文字。

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
    "recommendation": "详细推荐理由，50-100字，包含学校优势和报考建议",
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
- avgAdmissionScore = 近年有效分数的平均值（四舍五入取整）
- matchScore = 匹配分（0-100），100=远超录取线，50=持平，0=远低于
- minAdmissionScore / maxAdmissionScore = 近年录取线的最低/最高值
- 如果某年数据缺失，score 填 0
- 不同省份中考总分不同，请根据省份实际分析
- 禁止编造数据，搜不到就跳过该校
""";
    }

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

            var recommendations = JsonSerializer.Deserialize<List<HighSchoolRecommendation>>(json, JsonOptions);
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

        // 尝试直接找 JSON 数组
        var arrayStart = text.IndexOf('[');
        var arrayEnd = text.LastIndexOf(']');
        if (arrayStart >= 0 && arrayEnd > arrayStart)
        {
            return text[arrayStart..(arrayEnd + 1)];
        }

        // 尝试找 JSON 对象
        var objStart = text.IndexOf('{');
        var objEnd = text.LastIndexOf('}');
        if (objStart >= 0 && objEnd > objStart)
        {
            return text[objStart..(objEnd + 1)];
        }

        return null;
    }

    private const string SystemInstructions =
        """
        你是一位资深教育择校顾问，拥有丰富的中考志愿填报经验。你的任务是根据学生的摸底考试成绩和所在省份，
        通过联网搜索获取该省份各重点高中的历年录取分数线和详细办学信息，然后按"冲一冲、稳一稳、保一保"三档
        为学生推荐最适合报考的高中学校。

        【可用工具】
        - search_school_info(school_name, aspect?, region?)：学校专用联网搜索。
          school_name 必填。
          aspect 可选值：录取分数线 / 评价 / 师资 / 招生 / 排名。
          region 传入省份或城市用于消歧。
        - search_web(query, num?)：通用联网搜索。适用于搜索省份高中排名、录取分数线汇总等宏观信息。

        【强制调研流程】
        步骤 1：调用 search_web，搜索省份高中排名和省级示范高中名单，获取 15-20 所代表性高中。
               ⚠ 必须覆盖不同分数段的学校（高分段、中分段、低分段），不能只搜低分学校。
        步骤 2：对每所高中，分别调用：
               - search_school_info(aspect="录取分数线")：获取近 5 年录取线
               - search_school_info(aspect="评价")：获取学校特色、师资等信息
        步骤 3：将学校严格按以下标准分为三档，每档至少 3 所：
               - "冲"：学生分数 < 平均录取线，差距 5~40 分
               - "稳"：学生分数 ≈ 平均录取线（差距 -15~+15 分）
               - "保"：学生分数 > 平均录取线 + 15 分
               如果某档候选不足，必须扩大搜索范围补足。
        步骤 4：输出包含完整信息的 JSON 数组。

        【禁止事项】
        - 不得编造录取分数线数据
        - 搜不到数据的学校应跳过，不得用猜测数据
        - 不同省份中考总分不同，必须根据省份实际情况分析
        - 最终只输出严格 JSON 数组，不加 Markdown 代码块或过程说明
        - 每个分类（冲/稳/保）至少 3 所学校，这是硬性要求
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
