import { motion } from "framer-motion";
import { MapPin, Star, ChevronRight, Crown, Medal } from "lucide-react";
import type { SchoolRanking } from "@/types/school";
import { cn } from "@/lib/utils";

interface SchoolCardProps {
  ranking: SchoolRanking;
  onClick: () => void;
  index: number;
}

export function SchoolCard({ ranking, onClick, index }: SchoolCardProps) {
  const { school, analysis, rank, overallScore } = ranking;

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

  const badge = rankBadge();
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
      whileHover={{ y: -3, boxShadow: "0 8px 30px rgba(0,0,0,0.08)" }}
      onClick={onClick}
      className={cn(
        "relative p-5 rounded-2xl bg-white border cursor-pointer transition-all duration-200",
        rank === 1
          ? "border-gold-200 shadow-md"
          : "border-neutral-100 shadow-sm hover:border-warm-200",
      )}
    >
      <div className="flex items-start gap-4">
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
            <ScoreTag label="师资" score={analysis.teachingStaffScore} />
            <ScoreTag label="口碑" score={analysis.reputationScore} />
            <ScoreTag
              label="获奖"
              score={Math.min(analysis.awardsCount * 5, 100)}
            />
            <ScoreTag label="活跃" score={analysis.activityScore} />
          </div>
        </div>

        {/* 综合得分 */}
        <div className="flex flex-col items-center shrink-0 ml-2">
          <div className="flex items-center gap-0.5">
            <Star className={cn("w-3.5 h-3.5 fill-current", scoreColor)} />
          </div>
          <span className={cn("text-2xl font-bold font-display", scoreColor)}>
            {overallScore}
          </span>
          <span className="text-[10px] text-neutral-300">综合分</span>
        </div>
      </div>
    </motion.div>
  );
}

function ScoreTag({ label, score }: { label: string; score: number }) {
  const color =
    score >= 80
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
