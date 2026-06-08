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

public sealed record SessionUser(long Id, string Email, string DisplayName);

public sealed record AuthPayload(string Token, SessionUser User);

public sealed record CreateMessageRequest(string SchoolId, string SchoolName, string Content);

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
    IReadOnlyList<string> UniversityNames,
    IReadOnlyList<string>? PreferredMajors = null);

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