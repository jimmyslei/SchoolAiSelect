using SchoolRecommender.Api.Models;
using System.Text.Json;

namespace SchoolRecommender.Api.Services;

public sealed class SchoolRankingService
{
    public IReadOnlyList<SchoolRanking> RankSchools(
        IReadOnlyList<School> schools,
        IReadOnlyDictionary<string, SchoolAnalysis> analyses,
        WeightConfig? weights = null,
        int? studentScore = null)
    {
        var effectiveWeights = weights ?? WeightConfig.Default;
        var rankings = new List<SchoolRanking>();

        foreach (var school in schools)
        {
            if (!analyses.TryGetValue(school.Id, out var analysis))
            {
                continue;
            }

            var admissionMatch = studentScore.HasValue && SchoolTypes.IsMiddleSchool(school.Type)
                ? CalculateAdmissionMatchScore(analysis, studentScore.Value)
                : (int?)null;

            var score = CalculateOverallScore(analysis, school.Distance, effectiveWeights, admissionMatch);
            rankings.Add(new SchoolRanking(school, analysis, 0, score, string.Empty, false, admissionMatch));
        }

        rankings.Sort((left, right) => right.OverallScore.CompareTo(left.OverallScore));

        for (var index = 0; index < rankings.Count; index++)
        {
            var current = rankings[index];
            rankings[index] = current with
            {
                Rank = index + 1,
                IsTopPick = index == 0,
                Recommendation = BuildRecommendation(current.School, current.Analysis, index + 1, current.AdmissionMatchScore),
            };
        }

        return rankings;
    }

    public int CalculateOverallScore(School school, SchoolAnalysis analysis, WeightConfig? weights = null)
        => CalculateOverallScore(analysis, school.Distance, weights ?? WeightConfig.Default, null);

    private static int CalculateOverallScore(SchoolAnalysis analysis, int distance, WeightConfig weights, int? admissionMatchScore)
    {
        var distanceScore = Math.Max(0, (int)Math.Round((1 - (distance / 5000d)) * 100, MidpointRounding.AwayFromZero));
        var awardsScore = Math.Min(100, analysis.AwardsCount * 5);

        var total =
            analysis.TeachingStaffScore * weights.TeachingStaff +
            analysis.ReputationScore * weights.Reputation +
            awardsScore * weights.Awards +
            analysis.ActivityScore * weights.Activity +
            analysis.ContentQualityScore * weights.ContentQuality +
            analysis.StudentActivityScore * weights.StudentActivity +
            distanceScore * weights.Distance;

        // 高中：如果有录取匹配分，额外加权（占比 20%）
        if (admissionMatchScore.HasValue)
        {
            total = total * 0.8 + admissionMatchScore.Value * 0.2;
        }

        return (int)Math.Round(total, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 根据历年录取分数线和学生当前分数，计算录取匹配分 (0-100)。
    /// 学生分数高于最近一年录取线得 100 分，低于最早一年录取线得 0 分，中间线性插值。
    /// </summary>
    public int CalculateAdmissionMatchScore(SchoolAnalysis analysis, int studentScore)
    {
        if (string.IsNullOrWhiteSpace(analysis.AdmissionCutoffScores))
            return 50; // 无数据时给中间分

        try
        {
            var cutoffs = JsonSerializer.Deserialize<List<CutoffEntry>>(analysis.AdmissionCutoffScores);
            if (cutoffs is null || cutoffs.Count == 0)
                return 50;

            // 过滤有效数据（score > 0），按年份降序排列
            var validCutoffs = cutoffs
                .Where(c => c.Score > 0)
                .OrderByDescending(c => c.Year)
                .ToList();

            if (validCutoffs.Count == 0)
                return 50;

            // 取最近一年的录取线作为基准
            var latestCutoff = validCutoffs[0].Score;
            var oldestCutoff = validCutoffs[^1].Score;

            // 学生分数高于最新录取线
            if (studentScore >= latestCutoff)
                return Math.Min(100, 80 + (int)Math.Round((studentScore - latestCutoff) / (double)latestCutoff * 100));

            // 学生分数低于最早录取线
            if (studentScore <= oldestCutoff)
                return Math.Max(0, 20 - (int)Math.Round((oldestCutoff - studentScore) / (double)oldestCutoff * 100));

            // 线性插值：在 oldest 和 latest 之间
            var range = latestCutoff - oldestCutoff;
            if (range == 0) return 50;
            var ratio = (studentScore - oldestCutoff) / (double)range;
            return (int)Math.Round(20 + ratio * 60);
        }
        catch
        {
            return 50;
        }
    }

    private sealed record CutoffEntry(int Year, int Score);

    private static string BuildRecommendation(School school, SchoolAnalysis analysis, int rank, int? admissionMatchScore)
    {
        var strengths = new List<string>();

        if (analysis.TeachingStaffScore >= 80) strengths.Add("师资力量雄厚");
        if (analysis.ReputationScore >= 80) strengths.Add("家长口碑优秀");
        if (analysis.AwardsCount >= 10) strengths.Add("荣誉奖项丰富");
        if (analysis.ActivityScore >= 80) strengths.Add("信息公开活跃");
        if (analysis.ContentQualityScore >= 80) strengths.Add("内容质量出众");
        if (analysis.StudentActivityScore >= 80) strengths.Add("学生活动丰富");
        if (school.Distance <= 1000) strengths.Add("距离近便利通勤");

        var admissionNote = "";
        if (admissionMatchScore.HasValue)
        {
            admissionNote = admissionMatchScore.Value switch
            {
                >= 80 => "录取把握较大。",
                >= 60 => "有一定录取机会，建议作为冲刺目标。",
                >= 40 => "录取有一定难度，建议做好备选方案。",
                _ => "录取难度较高，建议优先考虑其他学校。",
            };
        }

        if (rank == 1)
        {
            return $"{school.Name}是本次综合评估最推荐的学校。{BuildStrengthSuffix(strengths)}整体指标表现较为均衡，适合作为优先考虑对象。{admissionNote}";
        }

        if (rank <= 3)
        {
            return $"{school.Name}综合表现优秀。{BuildStrengthSuffix(strengths)}属于值得重点比较的候选学校。{admissionNote}";
        }

        return $"{school.Name}{(strengths.Count > 0 ? "具备" + string.Join('、', strengths) + "等优势，" : string.Empty)}可作为备选学校继续观察。{admissionNote}";
    }

    private static string BuildStrengthSuffix(IReadOnlyList<string> strengths)
        => strengths.Count == 0 ? string.Empty : string.Join('、', strengths) + "，";
}