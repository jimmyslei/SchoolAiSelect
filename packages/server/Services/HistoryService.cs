using FreeSql;
using SchoolRecommender.Api.Data;
using SchoolRecommender.Api.Models;

namespace SchoolRecommender.Api.Services;

/// <summary>用户分析历史记录服务：保存、查询、删除</summary>
public sealed class HistoryService(IFreeSql freeSql)
{
    private const int MaxHistoryPerUser = 100;

    // ── 保存 ──────────────────────────────────────────────

    public async Task SaveAsync(
        long userId,
        string sessionType,
        string title,
        string requestJson,
        string resultJson,
        string? summary,
        CancellationToken cancellationToken = default)
    {
        var entity = new AnalysisHistory
        {
            UserId = userId,
            SessionType = sessionType,
            Title = title,
            RequestJson = requestJson,
            ResultJson = resultJson,
            Summary = summary,
            CreatedAt = DateTime.UtcNow,
        };

        await freeSql.Insert(entity).ExecuteAffrowsAsync(cancellationToken);

        // 超出上限时删除最旧的记录
        var count = await freeSql.Select<AnalysisHistory>()
            .Where(h => h.UserId == userId)
            .CountAsync(cancellationToken);

        if (count > MaxHistoryPerUser)
        {
            var overflow = (int)(count - MaxHistoryPerUser);
            var oldestIds = await freeSql.Select<AnalysisHistory>()
                .Where(h => h.UserId == userId)
                .OrderBy(h => h.CreatedAt)
                .Take(overflow)
                .ToListAsync(h => h.Id, cancellationToken);

            if (oldestIds.Count > 0)
            {
                await freeSql.Delete<AnalysisHistory>()
                    .Where(h => oldestIds.Contains(h.Id))
                    .ExecuteAffrowsAsync(cancellationToken);
            }
        }
    }

    // ── 列表 ──────────────────────────────────────────────

    public async Task<IReadOnlyList<AnalysisHistoryItem>> ListAsync(
        long userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        page = Math.Max(1, page);

        var rows = await freeSql.Select<AnalysisHistory>()
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return rows.Select(h => new AnalysisHistoryItem(
            h.Id,
            h.SessionType,
            h.Title,
            h.Summary,
            new DateTimeOffset(h.CreatedAt, TimeSpan.Zero)
        )).ToList();
    }

    public async Task<int> CountAsync(long userId, CancellationToken cancellationToken = default)
    {
        var count = await freeSql.Select<AnalysisHistory>()
            .Where(h => h.UserId == userId)
            .CountAsync(cancellationToken);
        return (int)count;
    }

    // ── 详情 ──────────────────────────────────────────────

    public async Task<AnalysisHistoryDetail?> GetDetailAsync(
        long id,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var h = await freeSql.Select<AnalysisHistory>()
            .Where(h => h.Id == id && h.UserId == userId)
            .FirstAsync(cancellationToken);

        if (h is null) return null;

        return new AnalysisHistoryDetail(
            h.Id,
            h.SessionType,
            h.Title,
            h.Summary,
            h.RequestJson,
            h.ResultJson,
            new DateTimeOffset(h.CreatedAt, TimeSpan.Zero));
    }

    // ── 删除 ──────────────────────────────────────────────

    public async Task<bool> DeleteAsync(long id, long userId, CancellationToken cancellationToken = default)
    {
        var affected = await freeSql.Delete<AnalysisHistory>()
            .Where(h => h.Id == id && h.UserId == userId)
            .ExecuteAffrowsAsync(cancellationToken);
        return affected > 0;
    }

    public async Task DeleteAllAsync(long userId, CancellationToken cancellationToken = default)
    {
        await freeSql.Delete<AnalysisHistory>()
            .Where(h => h.UserId == userId)
            .ExecuteAffrowsAsync(cancellationToken);
    }
}
