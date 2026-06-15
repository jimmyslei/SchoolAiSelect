/* ============================================
   学校推荐系统 - TypeScript 类型定义
   ============================================ */

/** 学校类型 */
export type SchoolType =
  | "kindergarten"
  | "primary"
  | "junior_high"
  | "senior_high";

export const SchoolTypeLabels: Record<SchoolType, string> = {
  kindergarten: "幼儿园",
  primary: "小学",
  junior_high: "初中",
  senior_high: "高中",
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
  /** 高中前5年录取分数线 JSON */
  admissionCutoffScores?: string;
}

/** 学校排名结果 */
export interface SchoolRanking {
  school: School;
  analysis: SchoolAnalysis;
  rank: number;
  overallScore: number;
  /** 推荐理由 */
  recommendation: string;
  /** 是否为首选推荐 */
  isTopPick: boolean;
  /** 高中录取匹配分 (0-100) */
  admissionMatchScore?: number;
  /** 前端流式状态：是否仍在分析中 */
  analyzing?: boolean;
}

/** 高中录取分数线 */
export interface AdmissionCutoffScore {
  year: number;
  score: number;
  schoolName?: string;
}

/** 搜索请求 */
export interface SearchRequest {
  latitude: number;
  longitude: number;
  schoolType: SchoolType;
  radius?: number; // 搜索半径，默认 5000 米
  studentScore?: number; // 高中搜索时学生分数
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

/** 登录用户 */
export interface SessionUser {
  id: number;
  email: string;
  displayName: string;
  isAdmin?: boolean;
}

/** 登录/注册响应 */
export interface AuthPayload {
  token: string;
  user: SessionUser;
  expiresAt: string;
}

/** 留言回复 */
export interface MessageReply {
  id: number;
  messageId: number;
  userId: number;
  authorName: string;
  content: string;
  createdAt: string;
}

/** 学校留言 */
export interface MessageThread {
  id: number;
  schoolId: string;
  schoolName: string;
  userId: number;
  authorName: string;
  content: string;
  createdAt: string;
  replies: MessageReply[];
}

/** 系统留言回复 */
export interface SiteMessageReply {
  id: number;
  messageId: number;
  userId: number;
  authorName: string;
  isAdmin: boolean;
  content: string;
  createdAt: string;
}

/** 系统留言 */
export interface SiteMessageThread {
  id: number;
  userId: number;
  authorName: string;
  content: string;
  createdAt: string;
  replies: SiteMessageReply[];
}

/** 搜索状态 */
export type SearchStatus =
  | "idle"
  | "selecting-type"
  | "locating"
  | "searching"
  | "streaming"
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

// ═══════════════════════════════════════════════════════════
// 大学报考相关类型
// ═══════════════════════════════════════════════════════════

/** 大学报考搜索请求 */
export interface UniversitySearchRequest {
  studentScore: number;
  universityNames: string[];
  preferredMajors?: string[];
}

/** 单年录取分数线 */
export interface UniversityAdmissionYear {
  year: number;
  minScore: number;
  avgScore?: number;
  maxScore?: number;
  note?: string;
}

/** 大学基本信息 */
export interface UniversityInfo {
  name: string;
  city?: string;
  province?: string;
  level?: string; // 985 / 211 / 双一流 / 普通本科
  type?: string; // 综合 / 理工 / 师范 / 财经 等
  website?: string;
}

/** 专业分析信息 */
export interface MajorAnalysis {
  majorName: string;
  category?: string; // 学科门类
  rank?: number; // 学科评估排名
  level?: string; // A+ / A / B+ 等
  description?: string;
  employmentRate?: number; // 就业率
  salaryIndex?: number; // 薪资指数
  strengths?: string;
  score: number; // 综合评分 0-100
}

/** 大学分析结果 */
export interface UniversityAnalysis {
  info: UniversityInfo;
  admissionScores: UniversityAdmissionYear[];
  majors: MajorAnalysis[];
  overallScore: number;
  admissionMatchScore: number;
  summary: string;
}

/** 大学报考排名结果 */
export interface UniversityRanking {
  analysis: UniversityAnalysis;
  rank: number;
  recommendation: string;
  isTopPick: boolean;
  isReachSchool: boolean; // 冲一冲
  isMatchSchool: boolean; // 稳一稳
  isSafetySchool: boolean; // 保一保
  /** 前端流式状态 */
  analyzing?: boolean;
}

/** 大学搜索状态 */
export type UniversitySearchStatus =
  | "idle"
  | "inputting"
  | "searching"
  | "streaming"
  | "success"
  | "error";

// ═══════════════════════════════════════════════════════════
// 高中择校推荐（按省份 + 分数，冲/稳/保）
// ═══════════════════════════════════════════════════════════

/** 高中择校推荐请求 */
export interface HighSchoolRecommendRequest {
  studentScore: number;
  province: string;
}

/** 高中单年录取分数线 */
export interface HighSchoolAdmissionYear {
  year: number;
  score: number;
}

/** 高中择校推荐项 */
export interface HighSchoolRecommendation {
  schoolName: string;
  city?: string;
  admissionScores: HighSchoolAdmissionYear[];
  /** "冲" | "稳" | "保" */
  category: string;
  /** 录取匹配分 0-100 */
  matchScore: number;
  /** 近年平均录取线 */
  avgAdmissionScore: number;
  /** 推荐理由 */
  recommendation: string;
  // ── 扩展信息 ──
  /** 学校级别：省级示范 / 市级重点 / 省重点 等 */
  schoolLevel?: string;
  /** 办学性质：公办 / 民办 */
  schoolNature?: string;
  /** 学校特色 */
  schoolFeatures?: string;
  /** 师资概况 */
  teachingStaffInfo?: string;
  /** 优势学科 */
  keyDisciplines?: string;
  /** 在校学生规模 */
  studentCount?: number;
  /** 学校详细地址 */
  address?: string;
  /** 学校官网 */
  website?: string;
  /** 招生计划人数 */
  enrollmentPlan?: number;
  /** 招生备注 */
  enrollmentNote?: string;
  /** 近年最低录取线 */
  minAdmissionScore: number;
  /** 近年最高录取线 */
  maxAdmissionScore: number;
}

/** 高中推荐搜索状态 */
export type HighSchoolRecommendStatus =
  | "idle"
  | "inputting"
  | "searching"
  | "streaming"
  | "success"
  | "error";
