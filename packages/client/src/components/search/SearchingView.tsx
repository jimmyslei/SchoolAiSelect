import { motion, AnimatePresence } from "framer-motion";
import {
  MapPin,
  Search,
  Sparkles,
  Loader2,
  Star,
  Crown,
  Medal,
} from "lucide-react";
import { useEffect, useState } from "react";
import { SchoolTypeLabels } from "@/types/school";
import type { SchoolRanking, SchoolType } from "@/types/school";
import { cn } from "@/lib/utils";

interface SearchingViewProps {
  schoolType: SchoolType;
  /** streaming 阶段已返回的学校列表（含 analyzing 标志） */
  rankings?: SchoolRanking[];
  analyzedCount?: number;
  totalCount?: number;
}

export function SearchingView({
  schoolType,
  rankings = [],
  analyzedCount = 0,
  totalCount = 0,
}: SearchingViewProps) {
  // 阶段文案：未拿到学校列表前显示"搜索中"，拿到后显示"分析中"
  const hasSchools = rankings.length > 0;
  const isAllDone = totalCount > 0 && analyzedCount >= totalCount;

  // 旋转动画 ticker
  const [tick, setTick] = useState(0);
  useEffect(() => {
    if (isAllDone) return;
    const id = setInterval(() => setTick((t) => t + 1), 600);
    return () => clearInterval(id);
  }, [isAllDone]);
  void tick; // suppress unused warning

  return (
    <motion.section
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      className="mx-auto max-w-3xl overflow-hidden rounded-4xl border border-border/40 bg-linear-to-br from-background via-warm-50/20 to-peach-50/20 p-6 shadow-lg backdrop-blur-sm sm:p-8"
    >
      {/* ── 顶部状态栏 ── */}
      <div className="flex items-center justify-between mb-6">
        <div className="inline-flex items-center gap-2 rounded-full bg-warm-100 px-4 py-2 text-xs font-medium text-warm-700">
          <Sparkles className="h-3.5 w-3.5" />
          {isAllDone
            ? "分析完成，生成排名中…"
            : hasSchools
              ? `正在分析 ${analyzedCount}/${totalCount} 所学校`
              : "搜索附近学校中…"}
        </div>

        {totalCount > 0 && (
          <span className="text-xs text-neutral-400">
            共找到 {totalCount} 所{SchoolTypeLabels[schoolType]}
          </span>
        )}
      </div>

      {/* ── 进度条 ── */}
      {totalCount > 0 && (
        <div className="mb-6">
          <div className="h-1.5 w-full rounded-full bg-neutral-100 overflow-hidden">
            <motion.div
              className="h-full rounded-full bg-linear-to-r from-warm-400 to-peach-400"
              initial={{ width: 0 }}
              animate={{
                width: `${Math.round((analyzedCount / totalCount) * 100)}%`,
              }}
              transition={{ duration: 0.4, ease: "easeOut" }}
            />
          </div>
          <div className="mt-1.5 flex justify-between text-[11px] text-neutral-400">
            <span>AI 分析进度</span>
            <span>{Math.round((analyzedCount / totalCount) * 100)}%</span>
          </div>
        </div>
      )}

      {/* ── 无学校时：旋转占位动画 ── */}
      {!hasSchools && (
        <div className="flex flex-col items-center py-10 gap-5">
          <div className="relative flex h-20 w-20 items-center justify-center rounded-full bg-linear-to-br from-warm-100 to-peach-100/60 shadow-sm">
            <motion.div
              animate={{ rotate: 360 }}
              transition={{ duration: 3.2, repeat: Infinity, ease: "linear" }}
              className="absolute inset-0 rounded-full border-4 border-transparent border-t-warm-500 border-r-peach-300"
            />
            <motion.div
              animate={{ rotate: -360 }}
              transition={{ duration: 4.8, repeat: Infinity, ease: "linear" }}
              className="absolute inset-2 rounded-full border-4 border-transparent border-b-gold-300 border-l-warm-200"
            />
            <Search className="relative h-7 w-7 text-warm-600" />
          </div>
          <p className="text-sm text-neutral-500">
            正在搜索附近的 {SchoolTypeLabels[schoolType]}…
          </p>
        </div>
      )}

      {hasSchools && !isAllDone && (
        <div className="mb-5 overflow-hidden rounded-3xl border border-warm-100/70 bg-warm-50/60 px-4 py-3">
          <div className="flex items-center gap-3">
            <div className="relative flex h-11 w-11 shrink-0 items-center justify-center">
              <motion.span
                aria-hidden
                className="absolute inset-0 rounded-full bg-warm-200/60"
                animate={{ scale: [0.9, 1.35], opacity: [0.5, 0] }}
                transition={{
                  duration: 1.8,
                  repeat: Infinity,
                  ease: "easeOut",
                }}
              />
              <motion.span
                aria-hidden
                className="absolute inset-1 rounded-full border border-peach-300/70"
                animate={{ scale: [0.9, 1.2], opacity: [0.45, 0] }}
                transition={{
                  duration: 1.8,
                  repeat: Infinity,
                  ease: "easeOut",
                  delay: 0.4,
                }}
              />
              <div className="relative flex h-8 w-8 items-center justify-center rounded-full bg-white/90 shadow-sm">
                <Loader2 className="h-4 w-4 animate-spin text-warm-500" />
              </div>
            </div>

            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2 text-sm font-medium text-neutral-800">
                <span>AI 分析中</span>
                <div className="flex items-center gap-1">
                  {[0, 1, 2].map((dot) => (
                    <motion.span
                      key={dot}
                      className="h-1.5 w-1.5 rounded-full bg-warm-400"
                      animate={{ y: [0, -4, 0], opacity: [0.35, 1, 0.35] }}
                      transition={{
                        duration: 1.1,
                        repeat: Infinity,
                        ease: "easeInOut",
                        delay: dot * 0.15,
                      }}
                    />
                  ))}
                </div>
              </div>
              <p className="mt-1 text-xs leading-5 text-neutral-500">
                正在逐所整理学校画像、口碑数据与综合匹配度，请稍候片刻。
              </p>
            </div>
          </div>
        </div>
      )}

      {/* ── 有学校时：实时卡片列表 ── */}
      {hasSchools && (
        <div className="space-y-2.5">
          <AnimatePresence initial={false}>
            {rankings.map((r, index) => (
              <SearchProgressCard key={r.school.id} ranking={r} index={index} />
            ))}
          </AnimatePresence>
        </div>
      )}
    </motion.section>
  );
}

// ── 单个学校进度卡片 ──────────────────────────────────────

interface SearchProgressCardProps {
  ranking: SchoolRanking;
  index: number;
}

function SearchProgressCard({ ranking, index }: SearchProgressCardProps) {
  const { school, analysis, overallScore, analyzing, admissionMatchScore } =
    ranking;

  const distanceText =
    school.distance >= 1000
      ? `${(school.distance / 1000).toFixed(1)} km`
      : `${school.distance} m`;

  const rankIcon = () => {
    if (!analyzing) {
      if (index === 0) return <Crown className="h-3.5 w-3.5 text-gold-500" />;
      if (index === 1)
        return <Medal className="h-3.5 w-3.5 text-neutral-400" />;
      if (index === 2) return <Medal className="h-3.5 w-3.5 text-warm-400" />;
    }
    return null;
  };

  const scoreColor =
    overallScore >= 80
      ? "text-sage-600"
      : overallScore >= 60
        ? "text-warm-600"
        : "text-neutral-400";

  return (
    <motion.div
      initial={{ opacity: 0, x: -12 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ duration: 0.35, delay: index * 0.04 }}
      className={cn(
        "flex items-center gap-3 rounded-2xl border px-4 py-3 transition-colors",
        analyzing
          ? "border-warm-100 bg-warm-50/40"
          : "border-neutral-100 bg-white/70",
      )}
    >
      {/* 序号 */}
      <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg bg-neutral-100 text-xs font-bold text-neutral-400">
        {index + 1}
      </div>

      {/* 学校名 + 地址 */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-1.5">
          <span className="text-sm font-medium text-neutral-800 truncate">
            {school.name}
          </span>
          {rankIcon()}
        </div>
        <div className="flex items-center gap-1 mt-0.5 text-neutral-400">
          <MapPin className="h-3 w-3 shrink-0" />
          <span className="text-[11px] truncate">{school.address}</span>
          <span className="text-[11px] shrink-0 text-warm-500 font-medium ml-1">
            {distanceText}
          </span>
        </div>
      </div>

      {/* 标签组 */}
      {!analyzing && (
        <div className="hidden sm:flex items-center gap-1 shrink-0">
          <MiniTag label="师资" score={analysis.teachingStaffScore} />
          <MiniTag label="口碑" score={analysis.reputationScore} />
          {admissionMatchScore != null && (
            <MiniTag label="匹配" score={admissionMatchScore} highlight />
          )}
        </div>
      )}

      {/* 得分 / loading */}
      <div className="flex shrink-0 flex-col items-center w-10">
        {analyzing ? (
          <Loader2 className="h-4 w-4 animate-spin text-warm-400" />
        ) : (
          <>
            <div className="flex items-center gap-0.5">
              <Star className={cn("h-3 w-3 fill-current", scoreColor)} />
            </div>
            <span
              className={cn(
                "text-base font-bold font-display leading-tight",
                scoreColor,
              )}
            >
              {overallScore}
            </span>
            <span className="text-[9px] text-neutral-300">综合分</span>
          </>
        )}
      </div>
    </motion.div>
  );
}

function MiniTag({
  label,
  score,
  highlight,
}: {
  label: string;
  score: number;
  highlight?: boolean;
}) {
  const color = highlight
    ? score >= 80
      ? "bg-sage-100 text-sage-700"
      : score >= 60
        ? "bg-gold-50 text-gold-600"
        : "bg-warm-50 text-warm-600"
    : score >= 80
      ? "bg-sage-50 text-sage-600"
      : score >= 60
        ? "bg-warm-50 text-warm-600"
        : "bg-neutral-50 text-neutral-500";

  return (
    <span
      className={cn("rounded px-1.5 py-0.5 text-[10px] font-medium", color)}
    >
      {label} {score}
    </span>
  );
}
