using System.Globalization;
using System.Text.Json.Serialization;

namespace SchoolRecommender.Api.Models;

public sealed record ApiResponse<T>(bool Success, T? Data, string? Message = null, bool FallbackToBrowser = false)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, data, message);

    public static ApiResponse<T> Fail(string message, bool fallbackToBrowser = false) => new(false, default, message, fallbackToBrowser);
}

public sealed record SearchRequest(double Latitude, double Longitude, string SchoolType, int? Radius);

public sealed record GeocodeRequest(string Address);

public sealed record ReverseGeocodeRequest(double Latitude, double Longitude);

public sealed record GeoLocationResponse(double Latitude, double Longitude, string Address);

public sealed record ReverseGeocodeResponse(string Address, string Province, string City, string District);

public sealed record IpLocationResponse(double Latitude, double Longitude, string Province, string City, string District, string Address);

public sealed record SearchResponseData(SearchLocation Location, IReadOnlyList<SchoolRanking> Rankings, int SearchRadius, int TotalFound);

public sealed record SearchLocation(string Address, double Latitude, double Longitude);

public sealed record School(string Id, string Name, string Type, string Address, double Latitude, double Longitude, int Distance, string? Phone = null, string? Website = null);

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
    IReadOnlyList<InfoSource> Sources);

public sealed record SchoolRanking(School School, SchoolAnalysis Analysis, int Rank, int OverallScore, string Recommendation, bool IsTopPick);

public sealed record WeightConfig(double TeachingStaff, double Reputation, double Awards, double Activity, double ContentQuality, double StudentActivity, double Distance)
{
    public static readonly WeightConfig Default = new(0.25, 0.2, 0.15, 0.15, 0.1, 0.1, 0.05);
}

public sealed record AmapPoi(string Id, string Name, string Address, string Location, string Distance, string? Tel);

public sealed record WechatArticle(string Title, string Url, string Summary, string PublishDate, string AccountName);

public sealed record XiaohongshuNote(string Id, string Title, string User, string LikeCount, string CollectCount, string CommentCount, string? NoteType = null);

public sealed record CollectedSchoolContent(
    string SchoolName,
    string SchoolType,
    string Address,
    IReadOnlyList<string> Queries,
    IReadOnlyList<WechatArticle> WechatArticles,
    IReadOnlyList<XiaohongshuNote> XiaohongshuNotes)
{
    public int WechatCount => WechatArticles.Count;

    public int XiaohongshuCount => XiaohongshuNotes.Count;

    public string? LatestWechatDate => WechatArticles
        .Select(article => DateOnly.TryParse(article.PublishDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? (DateOnly?)date : null)
        .OfType<DateOnly>()
        .OrderDescending()
        .Select(date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
        .FirstOrDefault();
}

public static class SchoolTypes
{
    public const string Kindergarten = "kindergarten";
    public const string Primary = "primary";
    public const string Middle = "middle";

    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        Kindergarten,
        Primary,
        Middle,
    };

    public static bool IsValid(string? schoolType) => !string.IsNullOrWhiteSpace(schoolType) && ValidTypes.Contains(schoolType);

    public static string DisplayName(string schoolType) => schoolType.ToLowerInvariant() switch
    {
        Kindergarten => "幼儿园",
        Primary => "小学",
        Middle => "中学",
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
            poi.Address,
            latitude,
            longitude,
            distance,
            string.IsNullOrWhiteSpace(poi.Tel) ? null : poi.Tel);
    }
}