import { useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import {
  GraduationCap,
  RotateCcw,
  TrendingUp,
  Shield,
  Target,
  ChevronDown,
  ChevronUp,
  MapPin,
  Building2,
  Users,
  BookOpen,
  Globe,
  Award,
  Sparkles,
  BarChart3,
} from "lucide-react";
import type { HighSchoolRecommendation } from "@/types/school";
import { Button } from "@/components/ui/button";

interface HighSchoolRecommendRankingProps {
  recommendations: HighSchoolRecommendation[];
  studentScore: number;
  province: string;
  onReset: () => void;
}

/** 分类配置 */
const CATEGORY_CONFIG: Record<
  string,
  {
    label: string;
    icon: typeof TrendingUp;
    color: string;
    bgColor: string;
    borderColor: string;
    desc: string;
    gradient: string;
  }
> = {
  冲: {
    label: "冲一冲",
    icon: TrendingUp,
    color: "text-orange-700",
    bgColor: "bg-orange-50",
    borderColor: "border-orange-200",
    desc: "录取有一定难度，但值得一试",
    gradient: "from-orange-400 to-red-400",
  },
  稳: {
    label: "稳一稳",
    icon: Target,
    color: "text-blue-700",
    bgColor: "bg-blue-50",
    borderColor: "border-blue-200",
    desc: "录取把握较大，推荐报考",
    gradient: "from-blue-400 to-indigo-400",
  },
  保: {
    label: "保一保",
    icon: Shield,
    color: "text-green-700",
    bgColor: "bg-green-50",
    borderColor: "border-green-200",
    desc: "录取把握很大，可作为保底",
    gradient: "from-green-400 to-emerald-400",
  },
};

/** 录取匹配分进度条 */
function MatchScoreBar({ score }: { score: number }) {
  const color =
    score >= 70
      ? "bg-green-500"
      : score >= 45
        ? "bg-blue-500"
        : "bg-orange-500";
  const textColor =
    score >= 70
      ? "text-green-700"
      : score >= 45
        ? "text-blue-700"
        : "text-orange-700";

  return (
    <div className="flex items-center gap-2">
      <div className="h-2 flex-1 overflow-hidden rounded-full bg-neutral-100">
        <motion.div
          initial={{ width: 0 }}
          animate={{ width: `${score}%` }}
          transition={{ duration: 0.8, ease: "easeOut" }}
          className={`h-full rounded-full ${color}`}
        />
      </div>
      <span className={`text-xs font-semibold ${textColor}`}>{score}%</span>
    </div>
  );
}

/** 分数趋势迷你图 */
function ScoreTrendChart({
  scores,
  studentScore,
}: {
  scores: { year: number; score: number }[];
  studentScore: number;
}) {
  const valid = scores
    .filter((s) => s.score > 0)
    .sort((a, b) => a.year - b.year);
  if (valid.length < 2) return null;

  const allScores = [...valid.map((s) => s.score), studentScore];
  const minVal = Math.min(...allScores) - 10;
  const maxVal = Math.max(...allScores) + 10;
  const range = maxVal - minVal || 1;
  const w = 200;
  const h = 60;

  const points = valid.map((s, i) => ({
    x: (i / (valid.length - 1)) * w,
    y: h - ((s.score - minVal) / range) * h,
  }));

  const pathD = points
    .map((p, i) => `${i === 0 ? "M" : "L"} ${p.x} ${p.y}`)
    .join(" ");
  const studentY = h - ((studentScore - minVal) / range) * h;

  return (
    <svg viewBox={`0 0 ${w} ${h}`} className="h-14 w-full">
      {/* 学生分数参考线 */}
      <line
        x1={0}
        y1={studentY}
        x2={w}
        y2={studentY}
        stroke="#a3a3a3"
        strokeWidth={1}
        strokeDasharray="4 3"
      />
      <text x={2} y={studentY - 3} fontSize={8} fill="#737373">
        你的分数({studentScore})
      </text>
      {/* 录取线趋势 */}
      <path
        d={pathD}
        fill="none"
        stroke="#6366f1"
        strokeWidth={2}
        strokeLinecap="round"
      />
      {points.map((p, i) => (
        <circle key={i} cx={p.x} cy={p.y} r={3} fill="#6366f1" />
      ))}
    </svg>
  );
}

/** 信息标签 */
function InfoTag({
  icon: Icon,
  label,
  value,
}: {
  icon: typeof Building2;
  label: string;
  value: string | number | undefined | null;
}) {
  if (!value) return null;
  return (
    <div className="flex items-start gap-1.5 text-xs">
      <Icon className="mt-0.5 h-3.5 w-3.5 shrink-0 text-neutral-400" />
      <span className="text-neutral-400">{label}：</span>
      <span className="text-neutral-600">{value}</span>
    </div>
  );
}

/** 单所高中推荐卡片 */
function SchoolCard({
  rec,
  studentScore,
}: {
  rec: HighSchoolRecommendation;
  studentScore: number;
}) {
  const [expanded, setExpanded] = useState(false);
  const config = CATEGORY_CONFIG[rec.category] ?? CATEGORY_CONFIG["稳"];
  const Icon = config.icon;

  const validScores = rec.admissionScores.filter((s) => s.score > 0);
  const scoreRange =
    rec.minAdmissionScore > 0 && rec.maxAdmissionScore > 0
      ? `${rec.minAdmissionScore}~${rec.maxAdmissionScore}`
      : null;
  const diff = studentScore - rec.avgAdmissionScore;

  return (
    <motion.div
      layout
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      className={`rounded-xl border bg-white/90 shadow-sm backdrop-blur-sm transition-shadow hover:shadow-md ${config.borderColor}`}
    >
      <div className="p-4 sm:p-5">
        {/* ── 头部：校名 + 城市 + 标签 ── */}
        <div className="mb-3 flex items-start justify-between">
          <div className="flex items-start gap-3">
            <div
              className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br ${config.gradient} text-white shadow-sm`}
            >
              <GraduationCap className="h-5.5 w-5.5" />
            </div>
            <div className="min-w-0">
              <h3 className="font-display text-lg font-semibold text-neutral-900">
                {rec.schoolName}
              </h3>
              <div className="mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-0.5">
                {rec.city && (
                  <span className="inline-flex items-center gap-0.5 text-xs text-neutral-500">
                    <MapPin className="h-3 w-3" />
                    {rec.city}
                  </span>
                )}
                {rec.schoolLevel && (
                  <span className="inline-flex items-center gap-0.5 rounded-full bg-amber-50 px-1.5 py-0.5 text-[10px] font-medium text-amber-700">
                    <Award className="h-2.5 w-2.5" />
                    {rec.schoolLevel}
                  </span>
                )}
                {rec.schoolNature && (
                  <span className="rounded-full bg-neutral-100 px-1.5 py-0.5 text-[10px] font-medium text-neutral-500">
                    {rec.schoolNature}
                  </span>
                )}
              </div>
            </div>
          </div>

          <div className="flex flex-col items-end gap-1.5">
            <span
              className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold ${config.bgColor} ${config.color}`}
            >
              <Icon className="h-3 w-3" />
              {config.label}
            </span>
            {rec.avgAdmissionScore > 0 && (
              <span className="text-xs text-neutral-400">
                平均线{" "}
                <span className="font-semibold text-neutral-700">
                  {rec.avgAdmissionScore}
                </span>
              </span>
            )}
          </div>
        </div>

        {/* ── 核心指标行：匹配度 + 分数差距 + 分数区间 ── */}
        <div className="mb-3 grid grid-cols-3 gap-2">
          <div className="rounded-lg bg-neutral-50 px-3 py-2">
            <p className="text-[10px] text-neutral-400">录取匹配度</p>
            <MatchScoreBar score={rec.matchScore} />
          </div>
          <div className="rounded-lg bg-neutral-50 px-3 py-2 text-center">
            <p className="text-[10px] text-neutral-400">与录取线差距</p>
            <p
              className={`text-sm font-bold ${
                diff >= 0 ? "text-green-600" : "text-orange-600"
              }`}
            >
              {diff >= 0 ? `+${diff}` : diff} 分
            </p>
          </div>
          <div className="rounded-lg bg-neutral-50 px-3 py-2 text-center">
            <p className="text-[10px] text-neutral-400">近年录取区间</p>
            <p className="text-sm font-bold text-neutral-700">
              {scoreRange || "—"}
            </p>
          </div>
        </div>

        {/* ── 学校特色信息 ── */}
        {(rec.schoolFeatures ||
          rec.teachingStaffInfo ||
          rec.keyDisciplines) && (
          <div className="mb-3 flex flex-wrap gap-x-4 gap-y-1 rounded-lg bg-gradient-to-r from-indigo-50/50 to-purple-50/50 px-3 py-2">
            {rec.schoolFeatures && (
              <div className="flex items-center gap-1 text-xs">
                <Sparkles className="h-3 w-3 text-indigo-400" />
                <span className="text-indigo-700">{rec.schoolFeatures}</span>
              </div>
            )}
            {rec.keyDisciplines && (
              <div className="flex items-center gap-1 text-xs">
                <BookOpen className="h-3 w-3 text-purple-400" />
                <span className="text-purple-700">
                  优势学科：{rec.keyDisciplines}
                </span>
              </div>
            )}
          </div>
        )}

        {/* ── 推荐理由 ── */}
        <p className="rounded-lg bg-neutral-50 px-3 py-2.5 text-sm leading-relaxed text-neutral-600">
          {rec.recommendation}
        </p>

        {/* ── 展开/收起 ── */}
        {(validScores.length > 0 ||
          rec.teachingStaffInfo ||
          rec.address ||
          rec.enrollmentNote) && (
          <button
            onClick={() => setExpanded(!expanded)}
            className="mt-3 flex w-full items-center justify-center gap-1 text-xs font-medium text-neutral-400 transition-colors hover:text-neutral-600"
          >
            {expanded ? (
              <>
                收起详情 <ChevronUp className="h-3.5 w-3.5" />
              </>
            ) : (
              <>
                查看完整信息 <ChevronDown className="h-3.5 w-3.5" />
              </>
            )}
          </button>
        )}
      </div>

      {/* ── 展开区：分数线表格 + 趋势图 + 详细信息 ── */}
      <AnimatePresence>
        {expanded && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: "auto", opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            className="overflow-hidden border-t border-neutral-100"
          >
            <div className="space-y-4 px-4 pb-4 pt-3 sm:px-5">
              {/* 分数趋势图 */}
              {validScores.length >= 2 && (
                <div>
                  <h4 className="mb-2 flex items-center gap-1.5 text-xs font-medium text-neutral-500">
                    <BarChart3 className="h-3.5 w-3.5" />
                    录取分数线趋势
                  </h4>
                  <div className="rounded-lg bg-white p-2">
                    <ScoreTrendChart
                      scores={validScores}
                      studentScore={studentScore}
                    />
                  </div>
                </div>
              )}

              {/* 历年分数线表格 */}
              {validScores.length > 0 && (
                <div>
                  <h4 className="mb-2 text-xs font-medium text-neutral-500">
                    近年录取分数线明细
                  </h4>
                  <div className="overflow-hidden rounded-lg border border-neutral-100">
                    <table className="w-full text-xs">
                      <thead className="bg-neutral-50">
                        <tr>
                          <th className="px-3 py-1.5 text-left font-medium text-neutral-500">
                            年份
                          </th>
                          <th className="px-3 py-1.5 text-right font-medium text-neutral-500">
                            录取线
                          </th>
                          <th className="px-3 py-1.5 text-right font-medium text-neutral-500">
                            差距
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        {validScores.map((s) => {
                          const d = studentScore - s.score;
                          return (
                            <tr
                              key={s.year}
                              className="border-t border-neutral-50"
                            >
                              <td className="px-3 py-1.5 font-medium text-neutral-700">
                                {s.year}
                              </td>
                              <td className="px-3 py-1.5 text-right font-semibold text-neutral-900">
                                {s.score}
                              </td>
                              <td
                                className={`px-3 py-1.5 text-right font-medium ${
                                  d >= 0 ? "text-green-600" : "text-orange-600"
                                }`}
                              >
                                {d >= 0 ? `+${d}` : d}
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}

              {/* 详细信息网格 */}
              <div className="grid gap-2 sm:grid-cols-2">
                <InfoTag
                  icon={Users}
                  label="师资"
                  value={rec.teachingStaffInfo}
                />
                <InfoTag
                  icon={Users}
                  label="学生规模"
                  value={rec.studentCount ? `约 ${rec.studentCount} 人` : null}
                />
                <InfoTag
                  icon={Building2}
                  label="招生计划"
                  value={rec.enrollmentPlan ? `${rec.enrollmentPlan} 人` : null}
                />
                <InfoTag
                  icon={BookOpen}
                  label="招生备注"
                  value={rec.enrollmentNote}
                />
                <InfoTag icon={MapPin} label="地址" value={rec.address} />
                {rec.website && (
                  <div className="flex items-start gap-1.5 text-xs">
                    <Globe className="mt-0.5 h-3.5 w-3.5 shrink-0 text-neutral-400" />
                    <span className="text-neutral-400">官网：</span>
                    <a
                      href={rec.website}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="truncate text-blue-600 underline-offset-2 hover:underline"
                    >
                      {rec.website}
                    </a>
                  </div>
                )}
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  );
}

/** 高中择校推荐结果列表 */
export function HighSchoolRecommendRanking({
  recommendations,
  studentScore,
  province,
  onReset,
}: HighSchoolRecommendRankingProps) {
  // 按分类分组
  const reachSchools = recommendations.filter((r) => r.category === "冲");
  const matchSchools = recommendations.filter((r) => r.category === "稳");
  const safetySchools = recommendations.filter((r) => r.category === "保");

  const sections = [
    { category: "冲", schools: reachSchools },
    { category: "稳", schools: matchSchools },
    { category: "保", schools: safetySchools },
  ].filter((s) => s.schools.length > 0);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-medium uppercase tracking-[0.24em] text-green-600">
            High School Recommendation
          </p>
          <h1 className="mt-2 font-display text-2xl font-semibold text-neutral-900 sm:text-3xl">
            高中择校推荐结果
          </h1>
          <p className="mt-1 text-sm text-neutral-500">
            <span className="font-medium">{province}</span> · 摸底成绩{" "}
            <span className="font-semibold text-neutral-700">
              {studentScore} 分
            </span>{" "}
            · 共推荐{" "}
            <span className="font-semibold text-neutral-700">
              {recommendations.length}
            </span>{" "}
            所高中
          </p>
        </div>
        <Button onClick={onReset} variant="outline" size="sm">
          <RotateCcw className="mr-1.5 h-3.5 w-3.5" />
          重新分析
        </Button>
      </div>

      {/* Category Summary Cards */}
      <div className="grid grid-cols-3 gap-3">
        {(["冲", "稳", "保"] as const).map((cat) => {
          const config = CATEGORY_CONFIG[cat];
          const Icon = config.icon;
          const count = recommendations.filter(
            (r) => r.category === cat,
          ).length;
          return (
            <div
              key={cat}
              className={`rounded-xl border p-3 text-center ${config.borderColor} ${config.bgColor}`}
            >
              <Icon className={`mx-auto h-5 w-5 ${config.color}`} />
              <p className={`mt-1 text-lg font-bold ${config.color}`}>
                {count}
              </p>
              <p className="text-xs text-neutral-500">{config.label}</p>
            </div>
          );
        })}
      </div>

      {/* School Lists by Category */}
      {sections.map(({ category, schools }) => {
        const config = CATEGORY_CONFIG[category];
        const Icon = config.icon;
        return (
          <div key={category}>
            <div className="mb-3 flex items-center gap-2">
              <div
                className={`flex h-7 w-7 items-center justify-center rounded-lg ${config.bgColor}`}
              >
                <Icon className={`h-4 w-4 ${config.color}`} />
              </div>
              <h2 className="font-display text-lg font-semibold text-neutral-800">
                {config.label}
              </h2>
              <span className="text-xs text-neutral-400">{config.desc}</span>
            </div>
            <div className="space-y-3">
              {schools.map((rec, i) => (
                <SchoolCard
                  key={`${category}-${i}`}
                  rec={rec}
                  studentScore={studentScore}
                />
              ))}
            </div>
          </div>
        );
      })}

      {/* Empty State */}
      {recommendations.length === 0 && (
        <div className="rounded-2xl border border-neutral-200 bg-white/80 px-6 py-12 text-center">
          <GraduationCap className="mx-auto h-12 w-12 text-neutral-300" />
          <p className="mt-3 text-neutral-500">暂无推荐结果</p>
        </div>
      )}
    </div>
  );
}
