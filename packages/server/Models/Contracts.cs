using System.Globalization;

namespace SchoolRecommender.Api.Models;

public sealed record ApiResponse<T>(bool Success, T? Data, string? Message = null, bool FallbackToBrowser = false)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, data, message);

    public static ApiResponse<T> Fail(string message, bool fallbackToBrowser = false) => new(false, default, message, fallbackToBrowser);
}

public sealed record SearchRequest(double Latitude, double Longitude, string SchoolType, int? Radius, int? StudentScore = null);

public sealed record GeocodeRequest(string Address);

public sealed record ReverseGeocodeRequest(double Latitude, double Longitude);

public sealed record GeoLocationResponse(double Latitude, double Longitude, string Address);

public sealed record SendRegisterCodeRequest(string Email);

public sealed record RegisterRequest(string Email, string Password, string DisplayName, string VerificationCode);

public sealed record LoginRequest(string Email, string Password);

public sealed record SessionUser(long Id, string Email, string DisplayName, bool IsAdmin = false);

public sealed record AuthPayload(string Token, SessionUser User, DateTimeOffset ExpiresAt);

public sealed record CreateMessageRequest(string SchoolId, string SchoolName, string Content);

public sealed record CreateSiteMessageRequest(string Content);

public sealed record SiteMessageReplyDto(long Id, long MessageId, long UserId, string AuthorName, bool IsAdmin, string Content, DateTimeOffset CreatedAt);

public sealed record SiteMessageThread(long Id, long UserId, string AuthorName, string Content, DateTimeOffset CreatedAt, IReadOnlyList<SiteMessageReplyDto> Replies);

public sealed record CreateReplyRequest(string Content);

public sealed record MessageReplyDto(long Id, long MessageId, long UserId, string AuthorName, string Content, DateTimeOffset CreatedAt);

public sealed record MessageThread(long Id, string SchoolId, string SchoolName, long UserId, string AuthorName, string Content, DateTimeOffset CreatedAt, IReadOnlyList<MessageReplyDto> Replies);

public sealed record ReverseGeocodeResponse(string Address, string Province, string City, string District);

public sealed record IpLocationResponse(double Latitude, double Longitude, string Province, string City, string District, string Address);

public sealed record SearchLocation(string Address, double Latitude, double Longitude);

public sealed record School(string Id, string Name, string Type, string CityName, string AdName, string Address, double Latitude, double Longitude, int Distance, string? Phone = null, string? Website = null);

public sealed record InfoSource(string Platform, string? AccountName, int PostCount, string? LastPostDate = null, string? Url = null);

public sealed record SchoolAnalysis(
    int ActivityScore,
    string LastPostDate,
    int ReputationScore,
    string ReputationSummary,
    int ContentQualityScore,
    int TeachingStaffScore,
    string TeachingStaffDesc,
    int AwardsCount,
    IReadOnlyList<string> AwardsDetail,
    int StudentActivityScore,
    string StudentActivityDesc,
    IReadOnlyList<InfoSource> Sources,
    string? AdmissionCutoffScores = null);

public sealed record SchoolRanking(School School, SchoolAnalysis Analysis, int Rank, int OverallScore, string Recommendation, bool IsTopPick, int? AdmissionMatchScore = null);

public sealed record WeightConfig(double TeachingStaff, double Reputation, double Awards, double Activity, double ContentQuality, double StudentActivity, double Distance)
{
    public static readonly WeightConfig Default = new(0.25, 0.2, 0.15, 0.15, 0.1, 0.1, 0.05);
}

public sealed record AmapPoi(string Id, string Name, string CityName, string PName, string AdName, string Address, string Location, string Distance, string? Tel);

public static class SchoolTypes
{
    public const string Kindergarten = "kindergarten";
    public const string Primary = "primary";
    public const string JuniorHigh = "junior_high";
    public const string SeniorHigh = "senior_high";

    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        Kindergarten,
        Primary,
        JuniorHigh,
        SeniorHigh,
    };

    public static bool IsValid(string? schoolType) => !string.IsNullOrWhiteSpace(schoolType) && ValidTypes.Contains(schoolType);

    /// <summary>是否为中学类型（初中/高中共用高德 POI 类别码）</summary>
    public static bool IsMiddleSchool(string schoolType)
        => string.Equals(schoolType, JuniorHigh, StringComparison.OrdinalIgnoreCase)
        || string.Equals(schoolType, SeniorHigh, StringComparison.OrdinalIgnoreCase);

    public static string DisplayName(string schoolType) => schoolType.ToLowerInvariant() switch
    {
        Kindergarten => "幼儿园",
        Primary => "小学",
        JuniorHigh => "初中",
        SeniorHigh => "高中",
        _ => schoolType,
    };
}

public static class SchoolMapper
{
    public static School FromPoi(AmapPoi poi, string schoolType)
    {
        var parts = poi.Location.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var longitude = parts.Length > 0 ? double.Parse(parts[0], CultureInfo.InvariantCulture) : 0;
        var latitude = parts.Length > 1 ? double.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
        var distance = int.TryParse(poi.Distance, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

        return new School(
            poi.Id,
            poi.Name,
            schoolType,
            poi.CityName,
            poi.AdName,
            poi.Address,
            latitude,
            longitude,
            distance,
            string.IsNullOrWhiteSpace(poi.Tel) ? null : poi.Tel);
    }
}

// ═══════════════════════════════════════════════════════════
// 大学报考相关类型
// ═══════════════════════════════════════════════════════════

/// <summary>大学报考搜索请求</summary>
public sealed record UniversitySearchRequest(
    int StudentScore,
    IReadOnlyList<string>? UniversityNames,
    IReadOnlyList<string>? PreferredMajors = null,
    IReadOnlyList<string>? PreferredDirections = null,
    string? PersonalPreferences = null,
    string? SearchMode = null);

/// <summary>单年录取分数线</summary>
public sealed record UniversityAdmissionYear(int Year, int MinScore, int? AvgScore = null, int? MaxScore = null, string? Note = null);

/// <summary>大学基本信息</summary>
public sealed record UniversityInfo(
    string Name,
    string? City,
    string? Province,
    string? Level,         // 985 / 211 / 双一流 / 普通本科
    string? Type,          // 综合 / 理工 / 师范 / 财经 等
    string? Website);

/// <summary>专业分析信息</summary>
public sealed record MajorAnalysis(
    string MajorName,
    string? Category,      // 学科门类
    int? Rank,             // 学科评估等级排名
    string? Level,         // A+ / A / B+ 等
    string? Description,
    int? EmploymentRate,   // 就业率
    int? SalaryIndex,      // 薪资指数
    string? Strengths,     // 专业优势
    string? TransferSupport,   // 是否支持转专业/政策宽松度
    string? EmploymentProspect, // 就业前景说明
    int Score = 0);        // 综合评分 0-100

/// <summary>大学分析结果（单所大学）</summary>
public sealed record UniversityAnalysis(
    UniversityInfo Info,
    IReadOnlyList<UniversityAdmissionYear> AdmissionScores,
    IReadOnlyList<MajorAnalysis> Majors,
    int OverallScore,              // 综合评分 0-100
    int AdmissionMatchScore,       // 录取匹配分 0-100
    string Summary);               // 综合评价摘要

/// <summary>大学报考排名结果</summary>
public sealed record UniversityRanking(
    UniversityAnalysis Analysis,
    int Rank,
    string Recommendation,
    bool IsTopPick,
    bool IsReachSchool,        // 冲一冲
    bool IsMatchSchool,        // 稳一稳
    bool IsSafetySchool);      // 保一保

// ═══════════════════════════════════════════════════════════
// 高中择校推荐（按省份 + 分数，冲/稳/保）
// ═══════════════════════════════════════════════════════════

/// <summary>高中择校推荐请求</summary>
public sealed record HighSchoolRecommendRequest(
    int StudentScore,
    string Province,
    string? City = null);

/// <summary>高中单年录取分数线</summary>
public sealed record HighSchoolAdmissionYear(int Year, double Score);

/// <summary>高中择校推荐项（单所高中）</summary>
public sealed record HighSchoolRecommendation(
    string SchoolName,
    string? City,
    IReadOnlyList<HighSchoolAdmissionYear> AdmissionScores,
    string Category,            // "冲" / "稳" / "保"
    int MatchScore,             // 录取匹配分 0-100
    int AvgAdmissionScore,      // 近年平均录取线
    string Recommendation,      // 推荐理由
    // ── 扩展信息 ──
    string? SchoolLevel,        // 学校级别：省级示范 / 市级重点 / 省重点 / 国家级示范 等
    string? SchoolNature,       // 办学性质：公办 / 民办
    string? SchoolFeatures,     // 学校特色，如"理科见长""外语特色""艺术体育强校"
    string? TeachingStaffInfo,  // 师资概况，如"特级教师5人，高级教师占比60%"
    string? KeyDisciplines,     // 优势学科，如"数学、物理"
    string? StudentCount,          // 在校学生规模
    string? Address,            // 学校详细地址
    string? Website,            // 学校官网
    string? EnrollmentPlan,        // 最新一年招生计划人数
    string? EnrollmentNote,     // 招生备注，如"面向全市招生""有住宿"
    double MinAdmissionScore,      // 近年最低录取线
    double MaxAdmissionScore);     // 近年最高录取线

/// <summary>Agent 流式执行步骤</summary>
public sealed record AgentExecutionStep(
    string Id,
    string Scope,
    string Status,
    string Message,
    DateTimeOffset Timestamp,
    string? EntityId = null,
    string? EntityName = null,
    string? ToolName = null,
    IReadOnlyDictionary<string, object?>? ToolArguments = null,
    string? ToolResult = null);

// ═══════════════════════════════════════════════════════════
// 历史分析记录
// ═══════════════════════════════════════════════════════════

/// <summary>历史记录列表项（不含完整结果）</summary>
public sealed record AnalysisHistoryItem(
    long Id,
    string SessionType,
    string Title,
    string? Summary,
    DateTimeOffset CreatedAt);

/// <summary>历史记录详情（含完整 done 结果 JSON）</summary>
public sealed record AnalysisHistoryDetail(
    long Id,
    string SessionType,
    string Title,
    string? Summary,
    string RequestJson,
    string ResultJson,
    DateTimeOffset CreatedAt);