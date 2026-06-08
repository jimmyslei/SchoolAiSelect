using SchoolRecommender.Api.Models;

namespace SchoolRecommender.Api.Services;

/// <summary>
/// 大学报考排名服务。
/// 根据录取匹配分、专业实力等维度对大学进行综合排名，并给出冲/稳/保建议。
/// </summary>
public sealed class UniversityRankingService
{
    /// <summary>
    /// 根据大学分析结果生成报考排名。
    /// </summary>
    public IReadOnlyList<UniversityRanking> RankUniversities(
        IReadOnlyList<UniversityAnalysis> analyses,
        int studentScore)
    {
        var rankings = new List<UniversityRanking>();

        foreach (var analysis in analyses)
        {
            // 综合评分 = 录取匹配分(50%) + 专业/学校实力(30%) + 就业前景(20%)
            var majorStrength = CalculateMajorStrength(analysis);
            var employmentScore = CalculateEmploymentScore(analysis);

            var compositeScore =
                analysis.AdmissionMatchScore * 0.5 +
                majorStrength * 0.3 +
                employmentScore * 0.2;

            var (isReach, isMatch, isSafety) = ClassifySchool(analysis.AdmissionMatchScore);

            rankings.Add(new UniversityRanking(
                Analysis: analysis with { OverallScore = (int)Math.Round(compositeScore) },
                Rank: 0,
                Recommendation: BuildRecommendation(analysis, isReach, isMatch, isSafety),
                IsTopPick: false,
                IsReachSchool: isReach,
                IsMatchSchool: isMatch,
                IsSafetySchool: isSafety));
        }

        // 按综合评分降序排列
        rankings.Sort((a, b) => b.Analysis.OverallScore.CompareTo(a.Analysis.OverallScore));

        // 分配排名
        for (var i = 0; i < rankings.Count; i++)
        {
            var current = rankings[i];
            rankings[i] = current with
            {
                Rank = i + 1,
                IsTopPick = i == 0,
            };
        }

        return rankings;
    }

    /// <summary>计算专业实力综合分 (0-100)</summary>
    private static int CalculateMajorStrength(UniversityAnalysis analysis)
    {
        if (analysis.Majors.Count == 0)
            return analysis.OverallScore;

        var avgScore = analysis.Majors.Average(m => m.Score);
        return (int)Math.Round(avgScore);
    }

    /// <summary>计算就业前景分 (0-100)</summary>
    private static int CalculateEmploymentScore(UniversityAnalysis analysis)
    {
        if (analysis.Majors.Count == 0)
            return 60;

        var majorsWithData = analysis.Majors
            .Where(m => m.EmploymentRate.HasValue || m.SalaryIndex.HasValue)
            .ToList();

        if (majorsWithData.Count == 0)
            return 60;

        var employmentAvg = majorsWithData
            .Where(m => m.EmploymentRate.HasValue)
            .Select(m => (double)m.EmploymentRate!.Value)
            .DefaultIfEmpty(60)
            .Average();

        var salaryAvg = majorsWithData
            .Where(m => m.SalaryIndex.HasValue)
            .Select(m => (double)m.SalaryIndex!.Value)
            .DefaultIfEmpty(60)
            .Average();

        return (int)Math.Round(employmentAvg * 0.6 + salaryAvg * 0.4);
    }

    /// <summary>根据录取匹配分将学校分类为冲/稳/保</summary>
    private static (bool IsReach, bool IsMatch, bool IsSafety) ClassifySchool(int admissionMatchScore)
    {
        // admissionMatchScore: 100 = 稳上，0 = 远不够
        if (admissionMatchScore >= 75)
            return (false, false, true);   // 保一保
        if (admissionMatchScore >= 45)
            return (false, true, false);   // 稳一稳
        return (true, false, false);       // 冲一冲
    }

    /// <summary>生成推荐理由</summary>
    private static string BuildRecommendation(UniversityAnalysis analysis, bool isReach, bool isMatch, bool isSafety)
    {
        var parts = new List<string>();

        if (isReach)
            parts.Add("【冲一冲】录取有一定难度，但值得一试");
        else if (isMatch)
            parts.Add("【稳一稳】录取把握较大，推荐报考");
        else if (isSafety)
            parts.Add("【保一保】录取把握很大，可作为保底选择");

        if (!string.IsNullOrWhiteSpace(analysis.Info.Level))
            parts.Add($"学校层次：{analysis.Info.Level}");

        if (analysis.Majors.Count > 0)
        {
            var topMajor = analysis.Majors.OrderByDescending(m => m.Score).First();
            if (!string.IsNullOrWhiteSpace(topMajor.Level))
                parts.Add($"优势专业 {topMajor.MajorName} 学科评估 {topMajor.Level}");
        }

        if (analysis.AdmissionScores.Count > 0)
        {
            var latest = analysis.AdmissionScores.First();
            parts.Add($"近年录取线约 {latest.MinScore} 分");
        }

        if (!string.IsNullOrWhiteSpace(analysis.Summary))
            parts.Add(analysis.Summary);

        return string.Join("；", parts);
    }
}
