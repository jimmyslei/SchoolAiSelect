/* ============================================
   学校推荐系统 - TypeScript 类型定义
   ============================================ */

/** 学校类型 */
export type SchoolType = "kindergarten" | "primary" | "middle";

export const SchoolTypeLabels: Record<SchoolType, string> = {
  kindergarten: "幼儿园",
  primary: "小学",
  middle: "中学",
};

/** 学校基本信息 */
export interface School {
  id: string;
  name: string;
  type: SchoolType;
  address: string;
  latitude: number;
  longitude: number;
  distance: number; // 与用户的距离，单位：米
  phone?: string;
  website?: string;
}

/** 信息来源 */
export interface InfoSource {
  platform: string; // 微信公众号 / 小红书 / 教育网站 等
  accountName?: string;
  postCount: number;
  lastPostDate?: string;
  url?: string;
}

/** 学校分析数据 - 多维度 */
export interface SchoolAnalysis {
  /** 官方账号发帖活跃度 (0-100) */
  activityScore: number;
  /** 最近发帖日期 */
  lastPostDate: string;
  /** 网络用户评价分 (0-100) */
  reputationScore: number;
  /** 用户评价摘要 */
  reputationSummary: string;
  /** 发帖文章质量分 (0-100) */
  contentQualityScore: number;
  /** 师资力量评分 (0-100) */
  teachingStaffScore: number;
  /** 师资描述 */
  teachingStaffDesc: string;
  /** 学校获得奖项数 */
  awardsCount: number;
  /** 获奖详情 */
  awardsDetail: string[];
  /** 学生活动及比赛获奖评分 (0-100) */
  studentActivityScore: number;
  /** 学生活动详情 */
  studentActivityDesc: string;
  /** 信息来源列表 */
  sources: InfoSource[];
}

/** 学校排名结果 */
export interface SchoolRanking {
  school: School;
  analysis: SchoolAnalysis;
  rank: number;
  overallScore: number; // 加权综合分 0-100
  recommendation: string; // 推荐理由
  isTopPick: boolean; // 是否为首选推荐
}

/** 搜索请求 */
export interface SearchRequest {
  latitude: number;
  longitude: number;
  schoolType: SchoolType;
  radius?: number; // 搜索半径，默认 5000 米
}

/** 搜索响应 */
export interface SearchResponse {
  success: boolean;
  data: {
    location: {
      address: string;
      latitude: number;
      longitude: number;
    };
    rankings: SchoolRanking[];
    searchRadius: number;
    totalFound: number;
  };
  message?: string;
}

/** 搜索状态 */
export type SearchStatus =
  | "idle"
  | "selecting-type"
  | "locating"
  | "searching"
  | "success"
  | "error";

/** 地理位置信息 */
export interface GeoLocation {
  latitude: number;
  longitude: number;
  address?: string;
  accuracy?: number;
}

/** 地理定位状态 */
export type GeoLocationStatus =
  | "idle"
  | "requesting"
  | "success"
  | "denied"
  | "error";

/** 维度权重配置 */
export interface WeightConfig {
  teachingStaff: number; // 师资 0.25
  reputation: number; // 评价 0.20
  awards: number; // 获奖 0.15
  activity: number; // 活跃度 0.15
  contentQuality: number; // 文章质量 0.10
  studentActivity: number; // 学生活动 0.10
  distance: number; // 距离 0.05
}

export const DEFAULT_WEIGHTS: WeightConfig = {
  teachingStaff: 0.25,
  reputation: 0.2,
  awards: 0.15,
  activity: 0.15,
  contentQuality: 0.1,
  studentActivity: 0.1,
  distance: 0.05,
};

/** 雷达图维度标签 */
export const DIMENSION_LABELS: Record<string, string> = {
  teachingStaffScore: "师资力量",
  reputationScore: "用户评价",
  contentQualityScore: "文章质量",
  activityScore: "网络活跃",
  studentActivityScore: "学生活动",
  awardsCount: "获奖荣誉",
};
