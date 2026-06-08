using System.Text.Json;
using FreeSql;
using SchoolRecommender.Api.Data;
using SchoolRecommender.Api.Models;

namespace SchoolRecommender.Api.Services;

public sealed class AnalyticsService(IFreeSql freeSql, ILogger<AnalyticsService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RecordVisitAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }

        var normalizedPath = NormalizePath(path);
        var now = DateTime.UtcNow;

        try
        {
            var existing = await freeSql.Select<SiteVisitStat>()
                .Where(stat => stat.Path == normalizedPath)
                .FirstAsync(cancellationToken);

            if (existing is null)
            {
                await freeSql.Insert(new SiteVisitStat
                {
                    Path = normalizedPath,
                    VisitCount = 1,
                    CreatedAt = now,
                    LastVisitedAt = now,
                }).ExecuteAffrowsAsync(cancellationToken);

                return;
            }

            await freeSql.Update<SiteVisitStat>()
                .Set(stat => stat.VisitCount + 1)
                .Set(stat => stat.LastVisitedAt, now)
                .Where(stat => stat.Id == existing.Id)
                .ExecuteAffrowsAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "记录站点访问失败: {Path}", normalizedPath);
        }
    }

    public async Task RecordSchoolSearchAsync(
        string searchSessionId,
        SearchRequest request,
        string? searchAddress,
        IReadOnlyList<SchoolRanking> rankings,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(searchSessionId) || rankings.Count == 0)
        {
            return;
        }

        var createdAt = DateTime.UtcNow;
        var logs = rankings.Select(ranking => new SchoolAnalysisLog
        {
            SearchSessionId = searchSessionId,
            SchoolId = ranking.School.Id,
            SchoolName = ranking.School.Name,
            SchoolType = ranking.School.Type,
            SearchLatitude = request.Latitude,
            SearchLongitude = request.Longitude,
            SearchRadius = request.Radius is > 0 ? request.Radius.Value : 5000,
            SearchAddress = TrimToNull(searchAddress, 512),
            SchoolDistance = ranking.School.Distance,
            OverallScore = ranking.OverallScore,
            Rank = ranking.Rank,
            IsTopPick = ranking.IsTopPick,
            ActivityScore = ranking.Analysis.ActivityScore,
            ReputationScore = ranking.Analysis.ReputationScore,
            ContentQualityScore = ranking.Analysis.ContentQualityScore,
            TeachingStaffScore = ranking.Analysis.TeachingStaffScore,
            AwardsCount = ranking.Analysis.AwardsCount,
            StudentActivityScore = ranking.Analysis.StudentActivityScore,
            SourceCount = ranking.Analysis.Sources.Count,
            AnalysisResultJson = JsonSerializer.Serialize(ranking.Analysis, JsonOptions),
            ClientIp = TrimToNull(clientIp, 64),
            UserAgent = TrimToNull(userAgent, 512),
            CreatedAt = createdAt,
        }).ToList();

        try
        {
            await freeSql.Insert(logs).ExecuteAffrowsAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "记录学校分析埋点失败: {SearchSessionId}", searchSessionId);
        }
    }

    private static string NormalizePath(string path)
    {
        var trimmed = path.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "/";
        }

        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }

    private static string? TrimToNull(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
