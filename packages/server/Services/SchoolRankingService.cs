using SchoolRecommender.Api.Models;

namespace SchoolRecommender.Api.Services;

public sealed class SchoolRankingService
{
    public IReadOnlyList<SchoolRanking> RankSchools(
        IReadOnlyList<School> schools,
        IReadOnlyDictionary<string, SchoolAnalysis> analyses,
        WeightConfig? weights = null)
    {
        var effectiveWeights = weights ?? WeightConfig.Default;
        var rankings = new List<SchoolRanking>();

        foreach (var school in schools)
        {
            if (!analyses.TryGetValue(school.Id, out var analysis))
            {
                continue;
            }

            var score = CalculateOverallScore(analysis, school.Distance, effectiveWeights);
            rankings.Add(new SchoolRanking(school, analysis, 0, score, string.Empty, false));
        }

        rankings.Sort((left, right) => right.OverallScore.CompareTo(left.OverallScore));

        for (var index = 0; index < rankings.Count; index++)
        {
            var current = rankings[index];
            rankings[index] = current with
            {
                Rank = index + 1,
                IsTopPick = index == 0,
                Recommendation = BuildRecommendation(current.School, current.Analysis, index + 1),
            };
        }

        return rankings;
    }

    private static int CalculateOverallScore(SchoolAnalysis analysis, int distance, WeightConfig weights)
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

        return (int)Math.Round(total, MidpointRounding.AwayFromZero);
    }

    private static string BuildRecommendation(School school, SchoolAnalysis analysis, int rank)
    {
        var strengths = new List<string>();

        if (analysis.TeachingStaffScore >= 80) strengths.Add("师资力量雄厚");
        if (analysis.ReputationScore >= 80) strengths.Add("家长口碑优秀");
        if (analysis.AwardsCount >= 10) strengths.Add("荣誉奖项丰富");
        if (analysis.ActivityScore >= 80) strengths.Add("信息公开活跃");
        if (analysis.ContentQualityScore >= 80) strengths.Add("内容质量出众");
        if (analysis.StudentActivityScore >= 80) strengths.Add("学生活动丰富");
        if (school.Distance <= 1000) strengths.Add("距离近便利通勤");

        if (rank == 1)
        {
            return $"{school.Name}是本次综合评估最推荐的学校。{BuildStrengthSuffix(strengths)}整体指标表现较为均衡，适合作为优先考虑对象。";
        }

        if (rank <= 3)
        {
            return $"{school.Name}综合表现优秀。{BuildStrengthSuffix(strengths)}属于值得重点比较的候选学校。";
        }

        return $"{school.Name}{(strengths.Count > 0 ? "具备" + string.Join('、', strengths) + "等优势，" : string.Empty)}可作为备选学校继续观察。";
    }

    private static string BuildStrengthSuffix(IReadOnlyList<string> strengths)
        => strengths.Count == 0 ? string.Empty : string.Join('、', strengths) + "，";
}