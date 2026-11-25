import { motion } from "framer-motion";
import {
  MapPin,
  Users,
  MessageCircle,
  Award,
  Globe,
  BookOpen,
  Activity,
  Star,
} from "lucide-react";
import type { SchoolRanking } from "@/types/school";
import { SchoolTypeLabels } from "@/types/school";
import { cn } from "@/lib/utils";

/** 对比维度配置 */
const COMPARISON_DIMENSIONS = [
  {
    key: "teachingStaffScore" as const,
    label: "师资力量",
    icon: Users,
    getScore: (r: SchoolRanking) => r.analysis.teachingStaffScore,
  },
  {
    key: "reputationScore" as const,
    label: "用户口碑",
    icon: MessageCircle,
    getScore: (r: SchoolRanking) => r.analysis.reputationScore,
  },
  {
    key: "contentQualityScore" as const,
    label: "内容质量",
    icon: BookOpen,
    getScore: (r: SchoolRanking) => r.analysis.contentQualityScore,
  },
  {
    key: "activityScore" as const,
    label: "网络活跃",
    icon: Globe,
    getScore: (r: SchoolRanking) => r.analysis.activityScore,
  },
  {
    key: "studentActivityScore" as const,
    label: "学生活动",
    icon: Activity,
    getScore: (r: SchoolRanking) => r.analysis.studentActivityScore,
  },
  {
    key: "awardsScore" as const,
    label: "获奖荣誉",
    icon: Award,
    getScore: (r: SchoolRanking) => Math.min(r.analysis.awardsCount * 5, 100),
  },
];

/** 获取分数颜色 */
function scoreColor(score: number): string {
  if (score >= 80) return "text-sage-600";
  if (score >= 60) return "text-warm-600";
  return "text-neutral-500";
}

/** 获取分数背景色 */
function scoreBg(score: number): string {
  if (score >= 80) return "bg-sage-50";
  if (score >= 60) return "bg-warm-50";
  return "bg-neutral-50";
}

/** 获取进度条颜色 */
function progressColor(score: number): string {
  if (score >= 80) return "bg-sage-500";
  if (score >= 60) return "bg-warm-500";
  return "bg-neutral-400";
}

interface ComparisonTableProps {
  rankings: SchoolRanking[];
}

/**
 * 学校对比表格：每行一个维度，每列一所学校，高亮最优项
 */
export function ComparisonTable({ rankings }: ComparisonTableProps) {
  // 找出每个维度的最高分
  const bestScores: Record<string, number> = {};
  COMPARISON_DIMENSIONS.forEach((dim) => {
    bestScores[dim.key] = Math.max(...rankings.map((r) => dim.getScore(r)));
  });

  return (
    <div className="overflow-x-auto -mx-1 px-1">
      <table className="w-full text-sm">
        {/* 表头：学校名称 */}
        <thead>
          <tr className="border-b border-border/70">
            <th className="py-3 pr-4 text-left text-xs font-medium text-neutral-500 w-28">
              维度
            </th>
            {rankings.map((r) => (
              <th
                key={r.school.id}
                className="py-3 px-3 text-center min-w-[120px]"
              >
                <div className="flex flex-col items-center gap-1">
                  {r.isTopPick && (
                    <span className="text-[10px] font-medium text-gold-600">
                      ⭐ 最佳
                    </span>
                  )}
                  <span className="font-semibold text-neutral-800 text-xs leading-tight">
                    {r.school.name}
                  </span>
                  <span className="text-[11px] text-neutral-400">
                    {SchoolTypeLabels[r.school.type]}
                  </span>
                </div>
              </th>
            ))}
          </tr>
        </thead>

        <tbody>
          {/* 综合评分行 */}
          <tr className="border-b border-border/50 bg-warm-50/30">
            <td className="py-3 pr-4 text-left">
              <div className="flex items-center gap-1.5">
                <Star className="w-3.5 h-3.5 text-warm-500" />
                <span className="font-medium text-neutral-700">综合评分</span>
              </div>
            </td>
            {rankings.map((r) => (
              <td key={r.school.id} className="py-3 px-3 text-center">
                <span
                  className={cn(
                    "text-xl font-bold font-display",
                    scoreColor(r.overallScore),
                  )}
                >
                  {r.overallScore}
                </span>
              </td>
            ))}
          </tr>

          {/* 距离行 */}
          <tr className="border-b border-border/50">
            <td className="py-3 pr-4 text-left">
              <div className="flex items-center gap-1.5">
                <MapPin className="w-3.5 h-3.5 text-neutral-400" />
                <span className="font-medium text-neutral-700">距离</span>
              </div>
            </td>
            {rankings.map((r) => {
              const dist =
                r.school.distance >= 1000
                  ? `${(r.school.distance / 1000).toFixed(1)}km`
                  : `${r.school.distance}m`;
              const isBest =
                r.school.distance ===
                Math.min(...rankings.map((x) => x.school.distance));
              return (
                <td key={r.school.id} className="py-3 px-3 text-center">
                  <span
                    className={cn(
                      "font-medium",
                      isBest ? "text-sage-600" : "text-neutral-600",
                    )}
                  >
                    {dist}
                  </span>
                </td>
              );
            })}
          </tr>

          {/* 各维度分数 */}
          {COMPARISON_DIMENSIONS.map((dim) => {
            const Icon = dim.icon;
            return (
              <tr
                key={dim.key}
                className="border-b border-border/50 hover:bg-neutral-50/50 transition-colors"
              >
                <td className="py-3 pr-4 text-left">
                  <div className="flex items-center gap-1.5">
                    <Icon className="w-3.5 h-3.5 text-neutral-400" />
                    <span className="font-medium text-neutral-700">
                      {dim.label}
                    </span>
                  </div>
                </td>
                {rankings.map((r) => {
                  const score = dim.getScore(r);
                  const isBest = score === bestScores[dim.key];
                  return (
                    <td key={r.school.id} className="py-3 px-3 text-center">
                      <div className="flex flex-col items-center gap-1.5">
                        <span
                          className={cn(
                            "font-semibold text-sm",
                            scoreColor(score),
                            isBest && "ring-2 ring-sage-200 rounded-md px-2",
                          )}
                        >
                          {score}
                        </span>
                        {/* 进度条 */}
                        <div className="w-full h-1.5 bg-neutral-100 rounded-full overflow-hidden">
                          <motion.div
                            initial={{ width: 0 }}
                            animate={{ width: `${score}%` }}
                            transition={{
                              duration: 0.6,
                              delay: 0.1,
                            }}
                            className={cn(
                              "h-full rounded-full",
                              progressColor(score),
                            )}
                          />
                        </div>
                      </div>
                    </td>
                  );
                })}
              </tr>
            );
          })}

          {/* 获奖详情 */}
          <tr className="border-b border-border/50">
            <td className="py-3 pr-4 text-left">
              <div className="flex items-center gap-1.5">
                <Award className="w-3.5 h-3.5 text-neutral-400" />
                <span className="font-medium text-neutral-700">获奖数量</span>
              </div>
            </td>
            {rankings.map((r) => {
              const isBest =
                r.analysis.awardsCount ===
                Math.max(...rankings.map((x) => x.analysis.awardsCount));
              return (
                <td key={r.school.id} className="py-3 px-3 text-center">
                  <span
                    className={cn(
                      "font-medium",
                      isBest ? "text-sage-600" : "text-neutral-600",
                    )}
                  >
                    {r.analysis.awardsCount} 项
                  </span>
                </td>
              );
            })}
          </tr>

          {/* 推荐理由 */}
          <tr>
            <td className="py-3 pr-4 text-left align-top">
              <span className="font-medium text-neutral-700 text-xs">
                推荐理由
              </span>
            </td>
            {rankings.map((r) => (
              <td key={r.school.id} className="py-3 px-3 text-left align-top">
                <p className="text-xs text-neutral-500 leading-relaxed">
                  {r.recommendation}
                </p>
              </td>
            ))}
          </tr>
        </tbody>
      </table>
    </div>
  );
}
