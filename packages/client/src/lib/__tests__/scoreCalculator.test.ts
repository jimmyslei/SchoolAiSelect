import { describe, it, expect } from "vitest";
import {
  calculateOverallScore,
  rerankWithWeights,
  normalizeWeights,
  DEFAULT_WEIGHTS,
} from "../scoreCalculator";
import type { SchoolAnalysis, SchoolRanking, School } from "@/types/school";

const mockSchool: School = {
  id: "test-1",
  name: "测试学校",
  type: "primary",
  address: "测试地址",
  latitude: 30.0,
  longitude: 120.0,
  distance: 1000,
};

const mockAnalysis: SchoolAnalysis = {
  teachingStaffScore: 80,
  reputationScore: 70,
  contentQualityScore: 65,
  activityScore: 75,
  awardsCount: 3,
  studentActivityScore: 60,
  teachingStaffDesc: "师资良好",
  reputationSummary: "口碑不错",
  lastPostDate: "2024-01-01",
  awardsDetail: ["奖项1", "奖项2", "奖项3"],
  studentActivityDesc: "活动丰富",
  sources: [],
};

describe("calculateOverallScore", () => {
  it("should calculate score with default weights", () => {
    const score = calculateOverallScore(mockAnalysis, 1000, DEFAULT_WEIGHTS);
    expect(score).toBeGreaterThan(0);
    expect(score).toBeLessThanOrEqual(100);
  });

  it("should return higher score for closer distance", () => {
    const scoreNear = calculateOverallScore(mockAnalysis, 100, DEFAULT_WEIGHTS);
    const scoreFar = calculateOverallScore(mockAnalysis, 4000, DEFAULT_WEIGHTS);
    expect(scoreNear).toBeGreaterThan(scoreFar);
  });

  it("should handle zero distance", () => {
    const score = calculateOverallScore(mockAnalysis, 0, DEFAULT_WEIGHTS);
    expect(score).toBeGreaterThan(0);
  });

  it("should handle distance over 5000m (distance score = 0)", () => {
    const score = calculateOverallScore(mockAnalysis, 6000, DEFAULT_WEIGHTS);
    expect(score).toBeGreaterThan(0);
  });

  it("should return higher score for more awards", () => {
    const highAwards = { ...mockAnalysis, awardsCount: 20 };
    const lowAwards = { ...mockAnalysis, awardsCount: 1 };
    const scoreHigh = calculateOverallScore(highAwards, 1000, DEFAULT_WEIGHTS);
    const scoreLow = calculateOverallScore(lowAwards, 1000, DEFAULT_WEIGHTS);
    expect(scoreHigh).toBeGreaterThan(scoreLow);
  });

  it("should cap awards score at 100", () => {
    const manyAwards = { ...mockAnalysis, awardsCount: 100 };
    const score = calculateOverallScore(manyAwards, 1000, DEFAULT_WEIGHTS);
    expect(score).toBeLessThanOrEqual(100);
  });
});

describe("normalizeWeights", () => {
  it("should normalize weights to sum to 1", () => {
    const weights = {
      teachingStaff: 0.5,
      reputation: 0.5,
      awards: 0.5,
      activity: 0.5,
      contentQuality: 0.5,
      studentActivity: 0.5,
      distance: 0.5,
    };
    const normalized = normalizeWeights(weights);
    const sum =
      normalized.teachingStaff +
      normalized.reputation +
      normalized.awards +
      normalized.activity +
      normalized.contentQuality +
      normalized.studentActivity +
      normalized.distance;
    expect(sum).toBeCloseTo(1, 2);
  });

  it("should return default weights when all are zero", () => {
    const weights = {
      teachingStaff: 0,
      reputation: 0,
      awards: 0,
      activity: 0,
      contentQuality: 0,
      studentActivity: 0,
      distance: 0,
    };
    const normalized = normalizeWeights(weights);
    expect(normalized).toEqual(DEFAULT_WEIGHTS);
  });
});

describe("rerankWithWeights", () => {
  it("should re-sort rankings by new weights", () => {
    const rankings: SchoolRanking[] = [
      {
        school: { ...mockSchool, id: "a", distance: 500 },
        analysis: { ...mockAnalysis, teachingStaffScore: 90 },
        rank: 1,
        overallScore: 80,
        recommendation: "推荐",
        isTopPick: true,
      },
      {
        school: { ...mockSchool, id: "b", distance: 500 },
        analysis: { ...mockAnalysis, teachingStaffScore: 50 },
        rank: 2,
        overallScore: 70,
        recommendation: "一般",
        isTopPick: false,
      },
    ];

    const distanceWeights = {
      teachingStaff: 0.05,
      reputation: 0.05,
      awards: 0.05,
      activity: 0.05,
      contentQuality: 0.05,
      studentActivity: 0.05,
      distance: 0.7,
    };

    const reranked = rerankWithWeights(rankings, distanceWeights);
    expect(reranked).toHaveLength(2);
    expect(reranked[0].rank).toBe(1);
    expect(reranked[1].rank).toBe(2);
  });
});
