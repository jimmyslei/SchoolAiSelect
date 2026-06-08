using FluentAssertions;
using SchoolRecommender.Api.Models;
using SchoolRecommender.Api.Services;
using Xunit;

namespace SchoolRecommender.Api.Tests.Services;

public class SchoolRankingServiceTests
{
    private readonly SchoolRankingService _service = new();

    private static School CreateSchool(string id, string name = "Test School", int distance = 1000)
        => new(id, name, "primary", "北京", "海淀区", "测试路1号",
            39.9, 116.4, distance);

    private static SchoolAnalysis CreateAnalysis(
        int teachingStaff = 70,
        int reputation = 70,
        int awardsCount = 5,
        int activity = 70,
        int contentQuality = 70,
        int studentActivity = 70) => new(
        ActivityScore: activity,
        LastPostDate: "2025-01-01",
        ReputationScore: reputation,
        ReputationSummary: "不错",
        ContentQualityScore: contentQuality,
        TeachingStaffScore: teachingStaff,
        TeachingStaffDesc: "良好",
        AwardsCount: awardsCount,
        AwardsDetail: ["区级优秀"],
        StudentActivityScore: studentActivity,
        StudentActivityDesc: "活跃",
        Sources: []);

    [Fact]
    public void RankSchools_ReturnsRankedByScoreDescending()
    {
        var schools = new[]
        {
            CreateSchool("1", "学校A", distance: 500),
            CreateSchool("2", "学校B", distance: 2000),
            CreateSchool("3", "学校C", distance: 3000),
        };
        var analyses = new Dictionary<string, SchoolAnalysis>
        {
            ["1"] = CreateAnalysis(teachingStaff: 90, reputation: 90),
            ["2"] = CreateAnalysis(teachingStaff: 60, reputation: 60),
            ["3"] = CreateAnalysis(teachingStaff: 75, reputation: 75),
        };

        var result = _service.RankSchools(schools, analyses);

        result.Should().HaveCount(3);
        result[0].Rank.Should().Be(1);
        result[0].School.Name.Should().Be("学校A");
        result[0].IsTopPick.Should().BeTrue();
        result[1].Rank.Should().Be(2);
        result[2].Rank.Should().Be(3);
        result[0].OverallScore.Should().BeGreaterThan(result[1].OverallScore);
        result[1].OverallScore.Should().BeGreaterThanOrEqualTo(result[2].OverallScore);
    }

    [Fact]
    public void RankSchools_OnlyIncludesSchoolsWithAnalysis()
    {
        var schools = new[]
        {
            CreateSchool("1", "学校A"),
            CreateSchool("2", "学校B"),
        };
        var analyses = new Dictionary<string, SchoolAnalysis>
        {
            ["1"] = CreateAnalysis(),
            // "2" missing from analyses
        };

        var result = _service.RankSchools(schools, analyses);

        result.Should().HaveCount(1);
        result[0].School.Id.Should().Be("1");
    }

    [Fact]
    public void RankSchools_EmptySchools_ReturnsEmpty()
    {
        var result = _service.RankSchools([], new Dictionary<string, SchoolAnalysis>());
        result.Should().BeEmpty();
    }

    [Fact]
    public void RankSchools_TopPick_IsFirstRanked()
    {
        var schools = new[] { CreateSchool("1") };
        var analyses = new Dictionary<string, SchoolAnalysis> { ["1"] = CreateAnalysis() };

        var result = _service.RankSchools(schools, analyses);

        result[0].IsTopPick.Should().BeTrue();
        result[0].Rank.Should().Be(1);
    }

    [Fact]
    public void CalculateOverallScore_HighScores_ReturnsHigh()
    {
        var school = CreateSchool("1", distance: 100);
        var analysis = CreateAnalysis(
            teachingStaff: 100, reputation: 100, awardsCount: 20,
            activity: 100, contentQuality: 100, studentActivity: 100);

        var score = _service.CalculateOverallScore(school, analysis);

        score.Should().BeGreaterThan(90);
    }

    [Fact]
    public void CalculateOverallScore_ZeroScores_ReturnsZero()
    {
        var school = CreateSchool("1", distance: 10000);
        var analysis = CreateAnalysis(
            teachingStaff: 0, reputation: 0, awardsCount: 0,
            activity: 0, contentQuality: 0, studentActivity: 0);

        var score = _service.CalculateOverallScore(school, analysis);

        // Distance score is max(0, (1 - 10000/5000) * 100) = 0, rest are 0
        score.Should().Be(0);
    }

    [Fact]
    public void CalculateOverallScore_NearbySchool_GetsDistanceBonus()
    {
        var nearSchool = CreateSchool("1", distance: 100);
        var farSchool = CreateSchool("2", distance: 4500);
        var analysis = CreateAnalysis();

        var nearScore = _service.CalculateOverallScore(nearSchool, analysis);
        var farScore = _service.CalculateOverallScore(farSchool, analysis);

        nearScore.Should().BeGreaterThan(farScore);
    }

    [Fact]
    public void CalculateOverallScore_ManyAwards_BoostsScore()
    {
        var school = CreateSchool("1", distance: 2000);
        var lowAwards = CreateAnalysis(awardsCount: 1);
        var highAwards = CreateAnalysis(awardsCount: 20);

        var lowScore = _service.CalculateOverallScore(school, lowAwards);
        var highScore = _service.CalculateOverallScore(school, highAwards);

        highScore.Should().BeGreaterThan(lowScore);
    }

    [Fact]
    public void RankSchools_Recommendation_MentionsStrengths()
    {
        var schools = new[] { CreateSchool("1", "名校A", distance: 500) };
        var analyses = new Dictionary<string, SchoolAnalysis>
        {
            ["1"] = CreateAnalysis(teachingStaff: 90, reputation: 85, awardsCount: 15),
        };

        var result = _service.RankSchools(schools, analyses);

        result[0].Recommendation.Should().Contain("名校A");
        result[0].Recommendation.Should().Contain("师资力量雄厚");
    }

    [Fact]
    public void RankSchools_CustomWeights_AffectsRanking()
    {
        var schools = new[]
        {
            CreateSchool("1", "学校A", distance: 1000),
            CreateSchool("2", "学校B", distance: 1000),
        };
        var analyses = new Dictionary<string, SchoolAnalysis>
        {
            // A: better teaching, B: better reputation
            ["1"] = CreateAnalysis(teachingStaff: 95, reputation: 50),
            ["2"] = CreateAnalysis(teachingStaff: 50, reputation: 95),
        };

        // Default weights: teaching 0.25 > reputation 0.20 → A wins
        var defaultResult = _service.RankSchools(schools, analyses);
        defaultResult[0].School.Name.Should().Be("学校A");

        // Reputation-heavy weights: B wins
        var customWeights = new WeightConfig(0.1, 0.5, 0.1, 0.1, 0.1, 0.05, 0.05);
        var customResult = _service.RankSchools(schools, analyses, customWeights);
        customResult[0].School.Name.Should().Be("学校B");
    }
}
