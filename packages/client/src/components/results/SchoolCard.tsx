import { motion } from "framer-motion";
import {
  MapPin,
  Star,
  ChevronRight,
  Crown,
  Medal,
  Loader2,
} from "lucide-react";
import type { SchoolRanking } from "@/types/school";
import { cn } from "@/lib/utils";

interface SchoolCardProps {
  ranking: SchoolRanking;
  onClick: () => void;
  index: number;
  /** 对比模式：是否显示勾选框 */
  compareMode?: boolean;
  /** 对比模式：是否已被选中 */
  isSelected?: boolean;
  /** 对比模式：勾选切换回调 */
  onToggleCompare?: () => void;
}

export function SchoolCard({
  ranking,
  onClick,
  index,
  compareMode,
  isSelected,
  onToggleCompare,
}: SchoolCardProps) {
  const {
    school,
    analysis,
    rank,
    overallScore,
    analyzing,
    admissionMatchScore,
  } = ranking;

  const rankBadge = () => {
    if (rank === 1)
      return {
        icon: Crown,
        color: "bg-gold-400 text-white",
        ring: "ring-gold-300",
      };
    if (rank === 2)
      return {
        icon: Medal,
        color: "bg-neutral-300 text-white",
        ring: "ring-neutral-200",
      };
    if (rank === 3)
      return {
        icon: Medal,
        color: "bg-warm-400 text-white",
        ring: "ring-warm-200",
      };
    return null;
  };

  const badge = analyzing ? null : rankBadge();
  const distanceText =
    school.distance >= 1000
      ? `${(school.distance / 1000).toFixed(1)}km`
      : `${school.distance}m`;

  // Score color
  const scoreColor =
    overallScore >= 80
      ? "text-sage-600"
      : overallScore >= 60
        ? "text-warm-600"
        : "text-neutral-500";

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, delay: index * 0.08 }}
      whileHover={
        analyzing
          ? undefined
          : { y: -3, boxShadow: "0 8px 30px rgba(0,0,0,0.08)" }
      }
      onClick={analyzing ? undefined : compareMode ? undefined : onClick}
      className={cn(
        "relative p-5 rounded-2xl bg-gradient-to-br from-card to-warm-50/30 border transition-all duration-300 backdrop-blur-sm",
        analyzing
          ? "border-warm-100 shadow-sm cursor-wait opacity-90"
          : compareMode
            ? "cursor-pointer"
            : "cursor-pointer",
        !analyzing && rank === 1 && !compareMode
          ? "border-gold-200/80 shadow-lg"
          : !analyzing
            ? isSelected
              ? "border-warm-300 shadow-md ring-2 ring-warm-200/50"
              : "border-border/60 shadow-sm hover:border-warm-200/70 hover:shadow-md"
            : "",
      )}
    >
      <div className="flex items-start gap-4">
        {/* 对比勾选框 */}
        {compareMode && !analyzing && (
          <div
            className="flex items-center shrink-0 pt-1"
            onClick={(e) => {
              e.stopPropagation();
              onToggleCompare?.();
            }}
          >
            <div
              className={cn(
                "w-5 h-5 rounded-md border-2 flex items-center justify-center transition-all",
                isSelected
                  ? "bg-warm-500 border-warm-500"
                  : "border-neutral-300 hover:border-warm-400",
              )}
            >
              {isSelected && (
                <svg
                  className="w-3 h-3 text-white"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                  strokeWidth={3}
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M5 13l4 4L19 7"
                  />
                </svg>
              )}
            </div>
          </div>
        )}

        {/* 排名 */}
        <div className="flex flex-col items-center shrink-0">
          {badge ? (
            <div
              className={cn(
                "w-10 h-10 rounded-xl flex items-center justify-center shadow-sm ring-2",
                badge.color,
                badge.ring,
              )}
            >
              <badge.icon className="w-5 h-5" />
            </div>
          ) : (
            <div className="w-10 h-10 rounded-xl bg-neutral-100 flex items-center justify-center">
              <span className="text-lg font-bold text-neutral-400">{rank}</span>
            </div>
          )}
        </div>

        {/* 学校信息 */}
        <div className="flex-1 min-w-0">
          <div className="flex items-start justify-between gap-2">
            <div>
              <h3 className="font-semibold text-neutral-800 text-base leading-tight">
                {school.name}
              </h3>
              <div className="flex items-center gap-1 mt-1 text-neutral-400">
                <MapPin className="w-3 h-3 shrink-0" />
                <span className="text-xs truncate">{school.address}</span>
                <span className="text-xs shrink-0 text-warm-500 font-medium ml-1">
                  {distanceText}
                </span>
              </div>
            </div>
            <ChevronRight className="w-5 h-5 text-neutral-300 shrink-0 mt-1" />
          </div>

          {/* 评分标签 */}
          <div className="flex flex-wrap gap-1.5 mt-3">
            {analyzing ? (
              <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-[11px] font-medium bg-warm-50 text-warm-600">
                <Loader2 className="w-3 h-3 animate-spin" />
                AI 分析中…
              </span>
            ) : (
              <>
                <ScoreTag label="师资" score={analysis.teachingStaffScore} />
                <ScoreTag label="口碑" score={analysis.reputationScore} />
                <ScoreTag
                  label="获奖"
                  score={Math.min(analysis.awardsCount * 5, 100)}
                />
                <ScoreTag label="活跃" score={analysis.activityScore} />
                {admissionMatchScore !== undefined &&
                  admissionMatchScore !== null && (
                    <ScoreTag
                      label="录取匹配"
                      score={admissionMatchScore}
                      highlight
                    />
                  )}
              </>
            )}
          </div>
        </div>

        {/* 综合得分 */}
        <div className="flex flex-col items-center shrink-0 ml-2">
          {analyzing ? (
            <>
              <Loader2 className="w-4 h-4 text-warm-400 animate-spin" />
              <span className="text-2xl font-bold font-display text-neutral-300">
                –
              </span>
              <span className="text-[10px] text-neutral-300">分析中</span>
            </>
          ) : (
            <>
              <div className="flex items-center gap-0.5">
                <Star className={cn("w-3.5 h-3.5 fill-current", scoreColor)} />
              </div>
              <span
                className={cn("text-2xl font-bold font-display", scoreColor)}
              >
                {overallScore}
              </span>
              <span className="text-[10px] text-neutral-300">综合分</span>
            </>
          )}
        </div>
      </div>
    </motion.div>
  );
}

function ScoreTag({
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
      ? "bg-sage-100 text-sage-700 ring-1 ring-sage-300"
      : score >= 60
        ? "bg-gold-50 text-gold-600 ring-1 ring-gold-200"
        : score >= 40
          ? "bg-warm-50 text-warm-600 ring-1 ring-warm-200"
          : "bg-red-50 text-red-500 ring-1 ring-red-200"
    : score >= 80
      ? "bg-sage-50 text-sage-600"
      : score >= 60
        ? "bg-warm-50 text-warm-600"
        : "bg-neutral-50 text-neutral-500";
  return (
    <span
      className={cn("px-2 py-0.5 rounded-md text-[11px] font-medium", color)}
    >
      {label} {score}
    </span>
  );
}
