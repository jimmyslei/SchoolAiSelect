/**
 * 前端综合评分计算
 * 与后端 SchoolRankingService.CalculateOverallScore 保持一致
 */

import type { SchoolAnalysis, SchoolRanking } from "@/types/school";

/** 权重配置（7 个维度，总和应为 1） */
export interface WeightConfig {
  teachingStaff: number;
  reputation: number;
  awards: number;
  activity: number;
  contentQuality: number;
  studentActivity: number;
  distance: number;
}

/** 默认权重 — 与后端 WeightConfig.Default 一致 */
export const DEFAULT_WEIGHTS: WeightConfig = {
  teachingStaff: 0.25,
  reputation: 0.2,
  awards: 0.15,
  activity: 0.15,
  contentQuality: 0.1,
  studentActivity: 0.1,
  distance: 0.05,
};

/** 预设权重模板 */
export const WEIGHT_PRESETS: Record<string, WeightConfig> = {
  balanced: { ...DEFAULT_WEIGHTS },
  重师资: {
    teachingStaff: 0.4,
    reputation: 0.15,
    awards: 0.1,
    activity: 0.1,
    contentQuality: 0.1,
    studentActivity: 0.05,
    distance: 0.1,
  },
  重口碑: {
    teachingStaff: 0.15,
    reputation: 0.4,
    awards: 0.1,
    activity: 0.1,
    contentQuality: 0.1,
    studentActivity: 0.1,
    distance: 0.05,
  },
  重距离: {
    teachingStaff: 0.15,
    reputation: 0.15,
    awards: 0.1,
    activity: 0.1,
    contentQuality: 0.05,
    studentActivity: 0.05,
    distance: 0.4,
  },
  重活动: {
    teachingStaff: 0.1,
    reputation: 0.15,
    awards: 0.15,
    activity: 0.15,
    contentQuality: 0.1,
    studentActivity: 0.3,
    distance: 0.05,
  },
};

/** 维度元数据 */
export const WEIGHT_DIMENSIONS = [
  { key: "teachingStaff" as const, label: "师资力量", color: "#ed8c42" },
  { key: "reputation" as const, label: "用户口碑", color: "#66b2a0" },
  { key: "awards" as const, label: "获奖荣誉", color: "#d4a843" },
  { key: "activity" as const, label: "网络活跃", color: "#6fa8d6" },
  { key: "contentQuality" as const, label: "内容质量", color: "#c27ba0" },
  { key: "studentActivity" as const, label: "学生活动", color: "#8e7cc3" },
  { key: "distance" as const, label: "距离远近", color: "#a8a49c" },
];

/**
 * 计算距离分数：5km 以外为 0，0m 为 100
 */
function calcDistanceScore(distanceMeters: number): number {
  return Math.max(0, Math.round((1 - distanceMeters / 5000) * 100));
}

/**
 * 计算获奖分数：每项获奖 5 分，上限 100
 */
function calcAwardsScore(awardsCount: number): number {
  return Math.min(100, awardsCount * 5);
}

/**
 * 使用指定权重计算综合评分
 * 与后端 SchoolRankingService.CalculateOverallScore 保持一致
 */
export function calculateOverallScore(
  analysis: SchoolAnalysis,
  distance: number,
  weights: WeightConfig,
): number {
  const distanceScore = calcDistanceScore(distance);
  const awardsScore = calcAwardsScore(analysis.awardsCount);

  const total =
    analysis.teachingStaffScore * weights.teachingStaff +
    analysis.reputationScore * weights.reputation +
    awardsScore * weights.awards +
    analysis.activityScore * weights.activity +
    analysis.contentQualityScore * weights.contentQuality +
    analysis.studentActivityScore * weights.studentActivity +
    distanceScore * weights.distance;

  return Math.round(total);
}

/**
 * 用新权重重新排序排名列表
 */
export function rerankWithWeights(
  rankings: SchoolRanking[],
  weights: WeightConfig,
): SchoolRanking[] {
  const reranked = rankings.map((r) => ({
    ...r,
    overallScore: calculateOverallScore(r.analysis, r.school.distance, weights),
  }));

  reranked.sort((a, b) => b.overallScore - a.overallScore);

  return reranked.map((r, i) => ({
    ...r,
    rank: i + 1,
    isTopPick: i === 0,
  }));
}

/**
 * 将权重归一化使总和为 1
 */
export function normalizeWeights(weights: WeightConfig): WeightConfig {
  const sum = Object.values(weights).reduce((s, w) => s + w, 0);
  if (sum === 0) return { ...DEFAULT_WEIGHTS };
  const factor = 1 / sum;
  return {
    teachingStaff: +(weights.teachingStaff * factor).toFixed(3),
    reputation: +(weights.reputation * factor).toFixed(3),
    awards: +(weights.awards * factor).toFixed(3),
    activity: +(weights.activity * factor).toFixed(3),
    contentQuality: +(weights.contentQuality * factor).toFixed(3),
    studentActivity: +(weights.studentActivity * factor).toFixed(3),
    distance: +(weights.distance * factor).toFixed(3),
  };
}
