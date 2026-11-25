import { motion } from "framer-motion";
import {
  ArrowLeft,
  MapPin,
  Star,
  Award,
  Users,
  BookOpen,
  MessageCircle,
  Calendar,
  Globe,
  ExternalLink,
} from "lucide-react";
import type { SchoolRanking } from "@/types/school";
import { SchoolTypeLabels } from "@/types/school";
import { SchoolRadarChart } from "./RadarChart";
import { NearbyFacilities } from "./NearbyFacilities";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";

interface SchoolDetailProps {
  ranking: SchoolRanking;
  onBack: () => void;
}

export function SchoolDetail({ ranking, onBack }: SchoolDetailProps) {
  const { school, analysis, rank, overallScore } = ranking;

  const distanceText =
    school.distance >= 1000
      ? `${(school.distance / 1000).toFixed(1)}km`
      : `${school.distance}m`;

  return (
    <motion.div
      initial={{ opacity: 0, x: 30 }}
      animate={{ opacity: 1, x: 0 }}
      exit={{ opacity: 0, x: -30 }}
      className="max-w-2xl mx-auto py-6 px-4"
    >
      {/* 返回按钮 */}
      <Button
        onClick={onBack}
        variant="ghost"
        size="sm"
        className="text-neutral-500 hover:text-warm-600 mb-4"
      >
        <ArrowLeft className="w-4 h-4" />
        返回排名列表
      </Button>

      {/* 学校头部 */}
      <div className="rounded-2xl border border-border/40 bg-gradient-to-br from-card to-warm-50/30 p-6 shadow-sm backdrop-blur-sm mb-4">
        <div className="flex items-start justify-between gap-4">
          <div>
            <div className="flex items-center gap-2 mb-1">
              <Badge variant="warm">
                #{rank} · {SchoolTypeLabels[school.type]}
              </Badge>
              {ranking.isTopPick && <Badge variant="gold">⭐ 最佳推荐</Badge>}
            </div>
            <h2 className="text-2xl font-bold text-neutral-800 font-display">
              {school.name}
            </h2>
            <div className="flex items-center gap-1 mt-2 text-neutral-400 text-sm">
              <MapPin className="w-3.5 h-3.5" />
              <span>{school.address}</span>
              <span className="text-warm-500 font-medium ml-1">
                {distanceText}
              </span>
            </div>
          </div>
          <div className="text-center shrink-0">
            <div className="text-4xl font-bold text-warm-600 font-display">
              {overallScore}
            </div>
            <div className="text-xs text-neutral-400">综合评分</div>
          </div>
        </div>

        {/* 推荐理由 */}
        <div className="mt-4 p-4 rounded-xl bg-gradient-to-r from-warm-50/80 to-peach-50/40 border border-warm-100/60">
          <p className="text-sm text-warm-800 leading-relaxed">
            {ranking.recommendation}
          </p>
        </div>
      </div>

      {/* 雷达图 */}
      <div className="rounded-2xl border border-border/40 bg-gradient-to-br from-card to-sand-50/30 p-6 shadow-sm backdrop-blur-sm mb-4">
        <SchoolRadarChart analysis={analysis} schoolName={school.name} />
      </div>

      {/* 详细指标 */}
      <div className="rounded-2xl border border-border/40 bg-gradient-to-br from-card to-warm-50/20 p-6 shadow-sm backdrop-blur-sm mb-4">
        <h3 className="font-semibold text-neutral-800 mb-4">详细分析</h3>
        <div className="space-y-4">
          <DetailItem
            icon={Users}
            label="师资力量"
            score={analysis.teachingStaffScore}
            description={analysis.teachingStaffDesc}
          />
          <DetailItem
            icon={MessageCircle}
            label="用户口碑"
            score={analysis.reputationScore}
            description={analysis.reputationSummary}
          />
          <DetailItem
            icon={BookOpen}
            label="内容质量"
            score={analysis.contentQualityScore}
            description={`发布文章内容专业性和可读性评分`}
          />
          <DetailItem
            icon={Globe}
            label="网络活跃度"
            score={analysis.activityScore}
            description={`最近发帖日期: ${analysis.lastPostDate}`}
          />
          <DetailItem
            icon={Award}
            label="获奖荣誉"
            score={Math.min(analysis.awardsCount * 5, 100)}
            description={`共获得 ${analysis.awardsCount} 项荣誉`}
          />
          <DetailItem
            icon={Star}
            label="学生活动"
            score={analysis.studentActivityScore}
            description={analysis.studentActivityDesc}
          />
        </div>
      </div>

      {/* 获奖详情 */}
      {analysis.awardsDetail.length > 0 && (
        <div className="rounded-2xl border border-border/40 bg-gradient-to-br from-card to-gold-50/20 p-6 shadow-sm backdrop-blur-sm mb-4">
          <h3 className="font-semibold text-neutral-800 mb-3">获奖记录</h3>
          <div className="space-y-2">
            {analysis.awardsDetail.map((award, i) => (
              <div
                key={i}
                className="flex items-center gap-2 text-sm text-neutral-600"
              >
                <Award className="w-4 h-4 text-gold-400 shrink-0" />
                <span>{award}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* 周边生活设施 */}
      <div className="rounded-2xl border border-border/40 bg-gradient-to-br from-card to-peach-50/20 p-6 shadow-sm backdrop-blur-sm mb-4">
        <NearbyFacilities
          latitude={school.latitude}
          longitude={school.longitude}
          schoolName={school.name}
        />
      </div>

      {/* 高中录取分数线 */}
      {analysis.admissionCutoffScores && (
        <div className="rounded-2xl border border-border/40 bg-gradient-to-br from-card to-warm-50/20 p-6 shadow-sm backdrop-blur-sm mb-4">
          <h3 className="font-semibold text-neutral-800 mb-3 flex items-center gap-2">
            <Award className="w-4 h-4 text-gold-500" />
            历年录取分数线
            {ranking.admissionMatchScore !== undefined &&
              ranking.admissionMatchScore !== null && (
                <span
                  className={cn(
                    "ml-auto text-sm font-medium px-2.5 py-0.5 rounded-full",
                    ranking.admissionMatchScore >= 80
                      ? "bg-sage-100 text-sage-700"
                      : ranking.admissionMatchScore >= 60
                        ? "bg-gold-50 text-gold-700"
                        : ranking.admissionMatchScore >= 40
                          ? "bg-warm-50 text-warm-700"
                          : "bg-red-50 text-red-600",
                  )}
                >
                  录取匹配 {ranking.admissionMatchScore}分
                </span>
              )}
          </h3>
          <AdmissionCutoffTable cutoffsJson={analysis.admissionCutoffScores} />
        </div>
      )}

      {/* 信息来源 */}
      {analysis.sources.length > 0 && (
        <div className="rounded-2xl border border-border/40 bg-gradient-to-br from-card to-sand-50/20 p-6 shadow-sm backdrop-blur-sm mb-8">
          <h3 className="font-semibold text-neutral-800 mb-3">信息来源</h3>
          <div className="space-y-2">
            {analysis.sources.map((source, i) => (
              <div
                key={i}
                className="flex items-center justify-between py-2 border-b border-border/50 last:border-0"
              >
                <div className="flex items-center gap-2">
                  <ExternalLink className="w-3.5 h-3.5 text-neutral-400" />
                  {source.url ? (
                    <a
                      href={source.url}
                      target="_blank"
                      rel="noreferrer"
                      className="text-sm text-neutral-700 underline-offset-2 transition-colors hover:text-warm-600 hover:underline"
                    >
                      {source.platform}
                    </a>
                  ) : (
                    <span className="text-sm text-neutral-700">
                      {source.platform}
                    </span>
                  )}
                  {source.accountName && (
                    <span className="text-xs text-neutral-400">
                      ({source.accountName})
                    </span>
                  )}
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-xs text-neutral-400">
                    {source.postCount} 篇内容
                  </span>
                  {source.url && (
                    <a
                      href={source.url}
                      target="_blank"
                      rel="noreferrer"
                      className="text-xs font-medium text-warm-600 transition-colors hover:text-warm-700"
                    >
                      查看来源
                    </a>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </motion.div>
  );
}

function DetailItem({
  icon: Icon,
  label,
  score,
  description,
}: {
  icon: typeof Users;
  label: string;
  score: number;
  description: string;
}) {
  const scoreColor =
    score >= 80
      ? "text-sage-600"
      : score >= 60
        ? "text-warm-600"
        : "text-neutral-500";
  const barColor =
    score >= 80
      ? "bg-sage-400"
      : score >= 60
        ? "bg-warm-400"
        : "bg-neutral-300";

  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Icon className="w-4 h-4 text-neutral-400" />
          <span className="text-sm font-medium text-neutral-700">{label}</span>
        </div>
        <span className={cn("text-sm font-bold", scoreColor)}>{score}</span>
      </div>
      <Progress value={score} className="h-2" />
      <p className="text-xs text-neutral-400">{description}</p>
    </div>
  );
}

function AdmissionCutoffTable({ cutoffsJson }: { cutoffsJson: string }) {
  let entries: { year: number; score: number }[] = [];
  try {
    entries = JSON.parse(cutoffsJson)
      .filter((e: { year: number; score: number }) => e.score > 0)
      .sort((a: { year: number }, b: { year: number }) => b.year - a.year);
  } catch {
    return <p className="text-sm text-neutral-400">录取分数线数据暂无法解析</p>;
  }

  if (entries.length === 0) {
    return <p className="text-sm text-neutral-400">暂无有效录取分数线数据</p>;
  }

  return (
    <div className="overflow-hidden rounded-xl border border-border/70">
      <table className="w-full text-sm">
        <thead>
          <tr className="bg-neutral-50 text-neutral-500">
            <th className="px-4 py-2.5 text-left font-medium">年份</th>
            <th className="px-4 py-2.5 text-right font-medium">录取分数线</th>
          </tr>
        </thead>
        <tbody>
          {entries.map((entry, i) => (
            <tr
              key={entry.year}
              className={i % 2 === 0 ? "bg-card" : "bg-neutral-50/50"}
            >
              <td className="px-4 py-2.5 text-neutral-700 font-medium">
                {entry.year} 年
              </td>
              <td className="px-4 py-2.5 text-right font-bold text-warm-600">
                {entry.score} 分
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
