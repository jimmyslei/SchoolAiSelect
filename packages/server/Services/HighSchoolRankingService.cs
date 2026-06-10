using SchoolRecommender.Api.Models;

namespace SchoolRecommender.Api.Services;

/// <summary>
/// 高中择校推荐排名服务。
/// 验证和规范化 Agent 返回的推荐结果，确保冲/稳/保分类正确且每档至少 3 所。
/// </summary>
public sealed class HighSchoolRankingService
{
    /// <summary>
    /// 规范化推荐结果：校正分类、计算匹配分、补全统计字段、动态平衡。
    /// </summary>
    public IReadOnlyList<HighSchoolRecommendation> Normalize(
        IReadOnlyList<HighSchoolRecommendation> recommendations,
        int studentScore)
    {
        // 第一步：计算匹配分和统计字段，按初始分类规则归类
        var scored = recommendations
            .Select(rec => RecalcWithFields(rec, studentScore))
            .ToList();

        // 第二步：动态平衡——如果某分类不足 3 所，调整阈值
        scored = Rebalance(scored, studentScore);

        // 第三步：按分类排序：冲 → 稳 → 保，同分类内按匹配分降序
        var categoryOrder = new Dictionary<string, int>
        {
            ["冲"] = 0,
            ["稳"] = 1,
            ["保"] = 2,
        };

        scored.Sort((a, b) =>
        {
            var catCmp = categoryOrder.GetValueOrDefault(a.Category, 1)
                .CompareTo(categoryOrder.GetValueOrDefault(b.Category, 1));
            return catCmp != 0 ? catCmp : b.MatchScore.CompareTo(a.MatchScore);
        });

        return scored;
    }

    /// <summary>
    /// 重新计算分类、匹配分和统计字段。
    /// </summary>
    private static HighSchoolRecommendation RecalcWithFields(
        HighSchoolRecommendation rec, int studentScore)
    {
        var validScores = rec.AdmissionScores
            .Where(s => s.Score > 0)
            .Select(s => s.Score)
            .ToList();

        var avgScore = validScores.Count > 0
            ? (int)Math.Round(validScores.Average())
            : rec.AvgAdmissionScore;

        var minScore = validScores.Count > 0 ? validScores.Min() : rec.MinAdmissionScore;
        var maxScore = validScores.Count > 0 ? validScores.Max() : rec.MaxAdmissionScore;

        var category = Classify(studentScore, avgScore);
        var matchScore = CalcMatchScore(studentScore, avgScore);

        return rec with
        {
            Category = category,
            MatchScore = matchScore,
            AvgAdmissionScore = avgScore,
            MinAdmissionScore = minScore,
            MaxAdmissionScore = maxScore,
        };
    }

    /// <summary>
    /// 初始分类：与 Agent 提示词对齐。
    /// 冲：差距 5~40 分 | 稳：±15 分 | 保：超过 15 分
    /// </summary>
    private static string Classify(int studentScore, int avgAdmission)
    {
        if (avgAdmission <= 0) return "稳"; // 无数据时归为稳

        var diff = studentScore - avgAdmission;

        if (diff >= 15)
            return "保";   // 学生分数明显高于录取线
        if (diff <= -5)
            return "冲";   // 学生分数低于录取线（有一定挑战）
        return "稳";       // 分数在录取线附近
    }

    /// <summary>
    /// 动态平衡：确保冲/稳/保每档至少有 minCount 所学校。
    /// 如果某档不足，从相邻档中按匹配分最接近的学校"借"过来。
    /// </summary>
    private static List<HighSchoolRecommendation> Rebalance(
        List<HighSchoolRecommendation> scored, int studentScore, int minCount = 3)
    {
        var reach = scored.Where(r => r.Category == "冲").ToList();
        var match = scored.Where(r => r.Category == "稳").ToList();
        var safety = scored.Where(r => r.Category == "保").ToList();

        // 如果"冲"不足，从"稳"中拉取最接近录取线（匹配分最低）的学校
        while (reach.Count < minCount && match.Count > minCount)
        {
            // 从"稳"中选匹配分最低的（最接近"冲"的）
            var candidate = match.OrderBy(r => r.MatchScore).First();
            match.Remove(candidate);
            reach.Add(candidate with { Category = "冲" });
        }

        // 如果"保"不足，从"稳"中拉取匹配分最高的学校
        while (safety.Count < minCount && match.Count > minCount)
        {
            var candidate = match.OrderByDescending(r => r.MatchScore).First();
            match.Remove(candidate);
            safety.Add(candidate with { Category = "保" });
        }

        // 如果"稳"不足，从"冲"或"保"中拉取
        while (match.Count < minCount)
        {
            // 优先从"保"中拉匹配分最低的
            if (safety.Count > minCount)
            {
                var candidate = safety.OrderBy(r => r.MatchScore).First();
                safety.Remove(candidate);
                match.Add(candidate with { Category = "稳" });
            }
            // 其次从"冲"中拉匹配分最高的
            else if (reach.Count > minCount)
            {
                var candidate = reach.OrderByDescending(r => r.MatchScore).First();
                reach.Remove(candidate);
                match.Add(candidate with { Category = "稳" });
            }
            else
            {
                break; // 总数不够，无法再平衡
            }
        }

        var result = new List<HighSchoolRecommendation>();
        result.AddRange(reach);
        result.AddRange(match);
        result.AddRange(safety);
        return result;
    }

    /// <summary>
    /// 计算匹配分 (0-100)。
    /// 100 = 远超录取线，50 = 持平，0 = 远低于录取线。
    /// </summary>
    private static int CalcMatchScore(int studentScore, int avgAdmission)
    {
        if (avgAdmission <= 0) return 50;

        var diff = studentScore - avgAdmission;
        // 每 1 分差距 ≈ 1 个匹配分，以 50 为基准
        return Math.Clamp(50 + diff, 0, 100);
    }
}
