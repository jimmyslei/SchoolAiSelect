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
/// 大学报考分析 Agent。
/// 根据用户的高考成绩、意向大学和意向专业，利用联网搜索获取近5年录取分数线
/// 和专业分析信息，输出结构化的大学报考排名推荐。
/// </summary>
public sealed class UniversityAnalysisAgent
{
    private readonly ChatClientAgent _agent;
    private readonly ILogger<UniversityAnalysisAgent> _logger;

    public UniversityAnalysisAgent(
        SchoolServiceOptions options,
        LocalSkillScriptRunner skillScriptRunner,
        ILoggerFactory loggerFactory,
        ILogger<UniversityAnalysisAgent> logger)
    {
        if (string.IsNullOrWhiteSpace(options.DashScopeApiKey))
            throw new InvalidOperationException("未配置 DASHSCOPE_API_KEY 或 OPENAI_API_KEY，无法初始化大学分析 Agent");

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
                "- aspect（可选）：搜索维度，可选值：录取分数线 / 专业 / 就业 / 排名 / 招生简章 / 学科评估。\n" +
                "- region（可选）：学校所在城市，用于消歧。\n" +
                "返回搜索结果列表。");

        var searchWebTool = AIFunctionFactory.Create(
            (string query, int? num, CancellationToken ct)
                => skillScriptRunner.SearchSchoolInfoAsync(query, null, null, ct),
            name: "search_web",
            description: "通用联网搜索工具。query 为搜索关键词，返回搜索结果列表。适用于搜索录取分数线、专业排名、就业数据等。");

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
                    Name = "UniversityAdmissionAnalyst",
                    Description = "分析大学录取分数线与专业信息，生成报考推荐排名。",
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
    /// 分析单所大学的录取分数线与专业信息。
    /// </summary>
    public async Task<UniversityAnalysis> AnalyzeUniversityAsync(
        string universityName,
        int studentScore,
        IReadOnlyList<string>? preferredMajors,
        CancellationToken cancellationToken)
    {
        var majorsText = preferredMajors is { Count: > 0 }
            ? $"意向专业：{string.Join("、", preferredMajors)}"
            : "不限定具体专业，综合分析该校热门/优势专业";

        var prompt = $$"""
请分析以下大学的报考信息，并输出结构化 JSON：

- 大学名称：{{universityName}}
- 学生摸底高考成绩：{{studentScore}} 分
- {{majorsText}}

请按以下步骤执行：
1. 搜索该校近 5 年（2021-2025）在各省的高考录取最低分数线（优先查找全国平均或主要省份数据）
2. 搜索该校的基本信息（所在城市、层次等级如 985/211/双一流、学校类型）
3. 搜索该校意向专业（或热门优势专业）的学科评估等级、就业率、薪资水平等信息
4. 综合评估该校对学生的录取匹配程度

最终输出以下 JSON 格式（不要输出其他内容）：
```json
{
  "info": {
    "name": "大学名称",
    "city": "所在城市",
    "province": "所在省份",
    "level": "985/211/双一流/普通本科",
    "type": "综合/理工/师范/财经/..."
  },
  "admissionScores": [
    {"year": 2025, "minScore": 600, "avgScore": 620, "note": "一本线"},
    {"year": 2024, "minScore": 595, "avgScore": 615},
    ...
  ],
  "majors": [
    {
      "majorName": "计算机科学与技术",
      "category": "工学",
      "rank": 5,
      "level": "A+",
      "description": "该专业...",
      "employmentRate": 95,
      "salaryIndex": 85,
      "strengths": "...",
      "score": 90
    }
  ],
  "overallScore": 85,
  "admissionMatchScore": 75,
  "summary": "综合评价摘要..."
}
```

注意事项：
- admissionScores：尽可能找到近 5 年数据，找不到的年份可跳过
- majors：如果用户指定了意向专业则分析这些专业，否则分析该校 3-5 个优势/热门专业
- overallScore：综合学科实力、就业前景等因素给出 0-100 分
- admissionMatchScore：根据学生分数与历年录取线的差距评估录取概率 (0-100)，100 表示稳上
- summary：300 字以内的综合评价
""";

        try
        {
            var response = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);
            var text = response.Text ?? string.Empty;
            _logger.LogDebug("大学分析 Agent 原始输出: {Text}", text);
            return ParseAnalysisResult(text, universityName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "大学分析失败: {University}", universityName);
            return CreateFallbackAnalysis(universityName);
        }
    }

    private UniversityAnalysis ParseAnalysisResult(string rawText, string universityName)
    {
        try
        {
            // 提取 JSON 块
            var json = ExtractJson(rawText);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("未能从 Agent 输出中提取 JSON，大学: {University}", universityName);
                return CreateFallbackAnalysis(universityName);
            }

            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var info = ParseUniversityInfo(root, universityName);
            var admissionScores = ParseAdmissionScores(root);
            var majors = ParseMajors(root);
            var overallScore = root.TryGetProperty("overallScore", out var os) ? os.GetInt32() : 60;
            var admissionMatch = root.TryGetProperty("admissionMatchScore", out var am) ? am.GetInt32() : 50;
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";

            return new UniversityAnalysis(info, admissionScores, majors, overallScore, admissionMatch, summary);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析大学分析结果失败: {University}", universityName);
            return CreateFallbackAnalysis(universityName);
        }
    }

    private static string? ExtractJson(string text)
    {
        // 尝试提取 ```json ... ``` 代码块
        var start = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (start >= 0)
        {
            start += 7;
            var end = text.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                return text[start..end].Trim();
        }

        // 尝试提取 ``` ... ``` 代码块
        start = text.IndexOf("```", StringComparison.Ordinal);
        if (start >= 0)
        {
            start += 3;
            // 跳过语言标记行
            var lineEnd = text.IndexOf('\n', start);
            if (lineEnd > start) start = lineEnd + 1;
            var end = text.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                return text[start..end].Trim();
        }

        // 尝试找第一个 { 到最后一个 }
        var braceStart = text.IndexOf('{');
        var braceEnd = text.LastIndexOf('}');
        if (braceStart >= 0 && braceEnd > braceStart)
            return text[braceStart..(braceEnd + 1)];

        return null;
    }

    private static UniversityInfo ParseUniversityInfo(JsonElement root, string fallbackName)
    {
        if (!root.TryGetProperty("info", out var infoElement))
            return new UniversityInfo(fallbackName, null, null, null, null, null);

        return new UniversityInfo(
            Name: infoElement.TryGetProperty("name", out var n) ? n.GetString() ?? fallbackName : fallbackName,
            City: infoElement.TryGetProperty("city", out var c) ? c.GetString() : null,
            Province: infoElement.TryGetProperty("province", out var p) ? p.GetString() : null,
            Level: infoElement.TryGetProperty("level", out var l) ? l.GetString() : null,
            Type: infoElement.TryGetProperty("type", out var t) ? t.GetString() : null,
            Website: infoElement.TryGetProperty("website", out var w) ? w.GetString() : null);
    }

    private static IReadOnlyList<UniversityAdmissionYear> ParseAdmissionScores(JsonElement root)
    {
        var result = new List<UniversityAdmissionYear>();
        if (!root.TryGetProperty("admissionScores", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in arr.EnumerateArray())
        {
            var year = item.TryGetProperty("year", out var y) ? y.GetInt32() : 0;
            var minScore = item.TryGetProperty("minScore", out var ms) ? ms.GetInt32() : 0;
            var avgScore = item.TryGetProperty("avgScore", out var avg) ? (int?)avg.GetInt32() : null;
            var maxScore = item.TryGetProperty("maxScore", out var mx) ? (int?)mx.GetInt32() : null;
            var note = item.TryGetProperty("note", out var nt) ? nt.GetString() : null;
            if (year > 0)
                result.Add(new UniversityAdmissionYear(year, minScore, avgScore, maxScore, note));
        }

        return result.OrderByDescending(s => s.Year).ToList();
    }

    private static IReadOnlyList<MajorAnalysis> ParseMajors(JsonElement root)
    {
        var result = new List<MajorAnalysis>();
        if (!root.TryGetProperty("majors", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in arr.EnumerateArray())
        {
            var majorName = item.TryGetProperty("majorName", out var mn) ? mn.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(majorName)) continue;

            result.Add(new MajorAnalysis(
                MajorName: majorName,
                Category: item.TryGetProperty("category", out var cat) ? cat.GetString() : null,
                Rank: item.TryGetProperty("rank", out var rank) ? rank.GetInt32() : null,
                Level: item.TryGetProperty("level", out var lv) ? lv.GetString() : null,
                Description: item.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                EmploymentRate: item.TryGetProperty("employmentRate", out var er) ? er.GetInt32() : null,
                SalaryIndex: item.TryGetProperty("salaryIndex", out var si) ? si.GetInt32() : null,
                Strengths: item.TryGetProperty("strengths", out var st) ? st.GetString() : null,
                Score: item.TryGetProperty("score", out var sc) ? sc.GetInt32() : 60));
        }

        return result;
    }

    private static UniversityAnalysis CreateFallbackAnalysis(string universityName)
    {
        return new UniversityAnalysis(
            new UniversityInfo(universityName, null, null, null, null, null),
            Array.Empty<UniversityAdmissionYear>(),
            Array.Empty<MajorAnalysis>(),
            OverallScore: 50,
            AdmissionMatchScore: 50,
            Summary: $"暂无法获取 {universityName} 的详细数据，建议进一步查询确认。");
    }

    private const string SystemInstructions = """
你是一个专业的高考志愿填报分析师。你的任务是：
1. 根据用户提供的大学名称和意向专业，利用联网搜索工具查找该校近 5 年的高考录取分数线
2. 查找该校的基本信息（城市、层次等级、学校类型）
3. 查找该校意向专业的学科评估、就业率、薪资水平等信息
4. 根据学生的摸底成绩，评估录取可能性
5. 输出结构化的 JSON 分析结果

重要规则：
- 必须使用联网搜索工具获取真实数据，不要编造数据
- 如果搜索不到某个年份的分数线，跳过该年份
- admissionMatchScore 的计算：学生分数高于最近一年录取线 30 分以上给 90+，高 10-30 分给 70-90，接近录取线给 50-70，低于录取线给 30-50，远低于给 0-30
- 所有评分使用 0-100 的整数
- 输出必须是合法的 JSON 格式
""";
}
