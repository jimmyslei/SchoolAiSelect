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
  BookOpen,
  Award,
  Briefcase,
} from "lucide-react";
import type {
  UniversityAnalysis,
  UniversityRanking,
  MajorAnalysis,
} from "@/types/school";
import { Button } from "@/components/ui/button";

interface UniversityRankingListProps {
  rankings: UniversityRanking[];
  analysesMap: Map<string, UniversityAnalysis>;
  analyzedCount: number;
  totalCount: number;
  studentScore: number;
  onReset: () => void;
}

/** 录取分类标签 */
function CategoryBadge({
  isReach,
  isMatch,
  isSafety,
}: {
  isReach: boolean;
  isMatch: boolean;
  isSafety: boolean;
}) {
  if (isReach)
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-orange-100 px-2.5 py-0.5 text-xs font-semibold text-orange-700">
        <TrendingUp className="h-3 w-3" /> 冲一冲
      </span>
    );
  if (isMatch)
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-blue-100 px-2.5 py-0.5 text-xs font-semibold text-blue-700">
        <Target className="h-3 w-3" /> 稳一稳
      </span>
    );
  if (isSafety)
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-semibold text-green-700">
        <Shield className="h-3 w-3" /> 保一保
      </span>
    );
  return null;
}

/** 录取匹配分进度条 */
function MatchScoreBar({ score }: { score: number }) {
  const color =
    score >= 75
      ? "bg-green-500"
      : score >= 45
        ? "bg-blue-500"
        : "bg-orange-500";
  const textColor =
    score >= 75
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

/** 录取分数线表格 */
function AdmissionTable({
  scores,
}: {
  scores: {
    year: number;
    minScore: number;
    avgScore?: number;
    note?: string;
  }[];
}) {
  if (scores.length === 0) return null;

  return (
    <div className="mt-3 overflow-hidden rounded-lg border border-neutral-100">
      <table className="w-full text-xs">
        <thead className="bg-neutral-50">
          <tr>
            <th className="px-3 py-1.5 text-left font-medium text-neutral-500">
              年份
            </th>
            <th className="px-3 py-1.5 text-right font-medium text-neutral-500">
              最低分
            </th>
            <th className="px-3 py-1.5 text-right font-medium text-neutral-500">
              平均分
            </th>
            <th className="px-3 py-1.5 text-left font-medium text-neutral-500">
              备注
            </th>
          </tr>
        </thead>
        <tbody>
          {scores.map((s) => (
            <tr key={s.year} className="border-t border-neutral-50">
              <td className="px-3 py-1.5 font-medium text-neutral-700">
                {s.year}
              </td>
              <td className="px-3 py-1.5 text-right font-semibold text-neutral-900">
                {s.minScore}
              </td>
              <td className="px-3 py-1.5 text-right text-neutral-600">
                {s.avgScore ?? "-"}
              </td>
              <td className="px-3 py-1.5 text-neutral-500">{s.note ?? ""}</td>
            </tr>
          ))}
        </tbody>
      </table>
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
    .filter((s) => s.score > 0 && Number.isFinite(s.score))
    .sort((a, b) => a.year - b.year);
  if (valid.length < 2) return null;
  // 防止 studentScore 为 0 或 NaN（初始值未赋值时）导致 min/max 计算异常
  const safeStudentScore =
    Number.isFinite(studentScore) && studentScore > 0
      ? studentScore
      : valid[valid.length - 1].score;

  const allScores = [...valid.map((s) => s.score), safeStudentScore];
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
  const studentY = h - ((safeStudentScore - minVal) / range) * h;

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
        你的分数({safeStudentScore})
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

/** 专业信息卡片 */
function MajorCard({ major }: { major: MajorAnalysis }) {
  return (
    <div className="rounded-lg border border-neutral-100 bg-white p-3">
      <div className="mb-1 flex items-center justify-between">
        <span className="text-sm font-medium text-neutral-800">
          {major.majorName}
        </span>
        {major.level && (
          <span className="rounded bg-purple-100 px-1.5 py-0.5 text-xs font-bold text-purple-700">
            {major.level}
          </span>
        )}
      </div>
      {major.category && (
        <p className="mb-1 text-xs text-neutral-500">{major.category}</p>
      )}
      <div className="flex flex-wrap gap-3 text-xs text-neutral-600">
        {major.employmentRate != null && (
          <span className="inline-flex items-center gap-1">
            <Briefcase className="h-3 w-3" />
            就业率 {major.employmentRate}%
          </span>
        )}
        {major.salaryIndex != null && (
          <span className="inline-flex items-center gap-1">
            <Award className="h-3 w-3" />
            薪资指数 {major.salaryIndex}
          </span>
        )}
        {major.rank != null && (
          <span className="inline-flex items-center gap-1">
            <TrendingUp className="h-3 w-3" />
            排名 #{major.rank}
          </span>
        )}
      </div>
      {major.strengths && (
        <p className="mt-1.5 text-xs leading-relaxed text-neutral-500">
          {major.strengths}
        </p>
      )}
    </div>
  );
}

/** 单所大学排名卡片 */
function UniversityCard({
  ranking,
  studentScore,
  isAnalyzing,
}: {
  ranking: UniversityRanking;
  studentScore: number;
  isAnalyzing?: boolean;
}) {
  const [expanded, setExpanded] = useState(false);
  const { analysis } = ranking;

  return (
    <motion.div
      layout
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      className={`rounded-xl border bg-white/90 shadow-sm backdrop-blur-sm transition-shadow hover:shadow-md ${
        ranking.isTopPick
          ? "border-warm-300 ring-1 ring-warm-200"
          : "border-neutral-200/80"
      }`}
    >
      {/* Header */}
      <div className="p-4 sm:p-5">
        <div className="mb-3 flex items-start justify-between">
          <div className="flex items-start gap-3">
            {/* Rank badge */}
            <div
              className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl text-lg font-bold ${
                ranking.rank === 1
                  ? "bg-gradient-to-br from-amber-400 to-orange-500 text-white"
                  : ranking.rank === 2
                    ? "bg-gradient-to-br from-neutral-300 to-neutral-400 text-white"
                    : ranking.rank === 3
                      ? "bg-gradient-to-br from-amber-600 to-amber-700 text-white"
                      : "bg-neutral-100 text-neutral-600"
              }`}
            >
              {ranking.rank}
            </div>
            <div>
              <h3 className="font-display text-lg font-semibold text-neutral-900">
                {analysis.info.name}
              </h3>
              <div className="mt-0.5 flex flex-wrap items-center gap-2 text-xs text-neutral-500">
                {analysis.info.city && (
                  <span className="inline-flex items-center gap-0.5">
                    <MapPin className="h-3 w-3" />
                    {analysis.info.province
                      ? `${analysis.info.province} ${analysis.info.city}`
                      : analysis.info.city}
                  </span>
                )}
                {analysis.info.level && (
                  <span className="rounded bg-warm-100 px-1.5 py-0.5 font-medium text-warm-700">
                    {analysis.info.level}
                  </span>
                )}
                {analysis.info.type && (
                  <span className="text-neutral-400">{analysis.info.type}</span>
                )}
              </div>
            </div>
          </div>

          <div className="flex flex-col items-end gap-1.5">
            <CategoryBadge
              isReach={ranking.isReachSchool}
              isMatch={ranking.isMatchSchool}
              isSafety={ranking.isSafetySchool}
            />
            <span className="text-xs text-neutral-400">
              综合评分{" "}
              <span className="font-semibold text-neutral-700">
                {analysis.overallScore}
              </span>
            </span>
          </div>
        </div>

        {/* Match Score */}
        <div className="mb-3">
          <div className="mb-1 flex items-center justify-between text-xs">
            <span className="text-neutral-500">录取匹配度</span>
            <span className="font-medium text-neutral-700">
              {analysis.admissionMatchScore >= 75
                ? "录取把握很大"
                : analysis.admissionMatchScore >= 45
                  ? "录取把握较大"
                  : "有一定难度"}
            </span>
          </div>
          <MatchScoreBar score={analysis.admissionMatchScore} />
        </div>

        {/* Recommendation */}
        <p className="rounded-lg bg-neutral-50 px-3 py-2 text-sm leading-relaxed text-neutral-600">
          {ranking.recommendation}
        </p>

        {/* Expand toggle */}
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
              查看详情 <ChevronDown className="h-3.5 w-3.5" />
            </>
          )}
        </button>
      </div>

      {/* Expanded Detail */}
      <AnimatePresence>
        {expanded && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: "auto", opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            className="overflow-hidden border-t border-neutral-100"
          >
            <div className="space-y-4 p-4 sm:p-5">
              {/* Admission Scores */}
              {analysis.admissionScores.length > 0 && (
                <div>
                  <h4 className="mb-1 flex items-center gap-1.5 text-sm font-medium text-neutral-700">
                    <TrendingUp className="h-4 w-4 text-warm-500" />
                    近年录取分数线
                  </h4>
                  {/* 趋势图 */}
                  <div className="rounded-lg bg-white p-2">
                    <ScoreTrendChart
                      scores={analysis.admissionScores.map((s) => ({
                        year: s.year,
                        score: s.minScore,
                      }))}
                      studentScore={studentScore}
                    />
                  </div>
                  <AdmissionTable scores={analysis.admissionScores} />
                </div>
              )}

              {/* Majors */}
              {analysis.majors.length > 0 && (
                <div>
                  <h4 className="mb-2 flex items-center gap-1.5 text-sm font-medium text-neutral-700">
                    <BookOpen className="h-4 w-4 text-purple-500" />
                    专业分析
                  </h4>
                  <div className="grid gap-2 sm:grid-cols-2">
                    {analysis.majors.map((major) => (
                      <MajorCard key={major.majorName} major={major} />
                    ))}
                  </div>
                </div>
              )}

              {/* Summary */}
              {analysis.summary && (
                <div className="rounded-lg bg-blue-50/50 px-3 py-2 text-sm text-neutral-700">
                  <span className="mr-1 font-medium text-blue-700">
                    AI 综合评价：
                  </span>
                  {analysis.summary}
                </div>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Analyzing overlay */}
      {isAnalyzing && (
        <div className="absolute inset-0 flex items-center justify-center rounded-xl bg-white/80 backdrop-blur-xs">
          <div className="flex items-center gap-2 text-sm text-neutral-500">
            <div className="h-4 w-4 animate-spin rounded-full border-2 border-purple-400 border-t-transparent" />
            分析中...
          </div>
        </div>
      )}
    </motion.div>
  );
}

/** 流式分析中的大学预览卡 */
function AnalyzingUniversityCard({ name }: { name: string }) {
  return (
    <div className="flex items-center gap-3 rounded-xl border border-dashed border-neutral-200 bg-white/60 p-4">
      <div className="h-5 w-5 animate-spin rounded-full border-2 border-purple-400 border-t-transparent" />
      <span className="text-sm text-neutral-500">正在分析 {name}...</span>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════
// Main Component
// ═══════════════════════════════════════════════════════════

export function UniversityRankingList({
  rankings,
  analysesMap,
  analyzedCount,
  totalCount,
  studentScore,
  onReset,
}: UniversityRankingListProps) {
  const isStreaming = totalCount > 0 && analyzedCount < totalCount;

  // 已有排名的大学名称集合
  const rankedNames = new Set(rankings.map((r) => r.analysis.info.name));

  // 仍在分析中的大学
  const pendingNames = Array.from(analysesMap.keys()).filter(
    (name) => !rankedNames.has(name),
  );

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-medium uppercase tracking-[0.24em] text-purple-600">
            University Analysis
          </p>
          <h2 className="mt-2 font-display text-2xl font-semibold text-neutral-900 sm:text-3xl">
            报考推荐排名
          </h2>
          {isStreaming ? (
            <p className="mt-1 text-sm text-neutral-500">
              已分析 {analyzedCount}/{totalCount} 所大学，请稍候...
            </p>
          ) : (
            <p className="mt-1 text-sm text-neutral-500">
              共分析 {rankings.length} 所大学，按录取概率和综合实力排序
            </p>
          )}
        </div>
        <Button variant="outline" size="sm" onClick={onReset}>
          <RotateCcw className="mr-1.5 h-3.5 w-3.5" />
          重新搜索
        </Button>
      </div>

      {/* Legend */}
      {rankings.length > 0 && (
        <div className="flex flex-wrap gap-3 text-xs text-neutral-500">
          <span className="flex items-center gap-1">
            <div className="h-2 w-2 rounded-full bg-orange-400" />
            冲一冲：录取有难度但值得一试
          </span>
          <span className="flex items-center gap-1">
            <div className="h-2 w-2 rounded-full bg-blue-400" />
            稳一稳：录取把握较大
          </span>
          <span className="flex items-center gap-1">
            <div className="h-2 w-2 rounded-full bg-green-400" />
            保一保：录取把握很大
          </span>
        </div>
      )}

      {/* Rankings */}
      <div className="space-y-4">
        <AnimatePresence>
          {rankings.map((ranking) => (
            <UniversityCard
              key={ranking.analysis.info.name}
              ranking={ranking}
              studentScore={studentScore}
              isAnalyzing={false}
            />
          ))}
        </AnimatePresence>

        {/* Still analyzing cards */}
        {pendingNames.map((name) => (
          <AnalyzingUniversityCard key={name} name={name} />
        ))}
      </div>

      {/* Empty state */}
      {!isStreaming && rankings.length === 0 && (
        <div className="py-12 text-center text-neutral-400">
          <GraduationCap className="mx-auto mb-3 h-12 w-12 opacity-50" />
          <p>暂无分析结果</p>
        </div>
      )}
    </div>
  );
}
