import { motion } from "framer-motion";
import { Trophy, Sparkles, Star } from "lucide-react";
import type { SchoolRanking } from "@/types/school";

interface RecommendBannerProps {
  topSchool: SchoolRanking;
}

export function RecommendBanner({ topSchool }: RecommendBannerProps) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.6 }}
      className="relative overflow-hidden rounded-2xl bg-linear-to-r from-gold-100 via-warm-100 to-peach-100 border border-gold-200/50 p-6 sm:p-8"
    >
      {/* 装饰 */}
      <div className="absolute top-0 right-0 w-32 h-32 bg-linear-to-bl from-gold-200/40 to-transparent rounded-bl-full" />
      <div className="absolute -bottom-4 -left-4 w-24 h-24 bg-linear-to-tr from-warm-200/30 to-transparent rounded-full blur-xl" />

      <div className="relative flex flex-col sm:flex-row sm:items-center gap-4">
        {/* 奖杯 */}
        <motion.div
          initial={{ scale: 0, rotate: -20 }}
          animate={{ scale: 1, rotate: 0 }}
          transition={{ type: "spring", delay: 0.3, bounce: 0.5 }}
          className="w-16 h-16 rounded-2xl bg-linear-to-br from-gold-300 to-gold-500 flex items-center justify-center shadow-lg shadow-gold-300/30 shrink-0"
        >
          <Trophy className="w-8 h-8 text-white" />
        </motion.div>

        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1">
            <Sparkles className="w-4 h-4 text-gold-500" />
            <span className="text-xs font-medium text-gold-600 uppercase tracking-wide">
              最佳推荐
            </span>
          </div>
          <h3 className="text-xl sm:text-2xl font-bold text-neutral-800 font-display">
            {topSchool.school.name}
          </h3>
          <p className="text-sm text-neutral-500 mt-1.5 leading-relaxed">
            {topSchool.recommendation}
          </p>
        </div>

        {/* 综合评分 */}
        <div className="flex flex-col items-center shrink-0 sm:border-l sm:border-gold-200/50 sm:pl-6">
          <div className="flex items-center gap-1 mb-0.5">
            <Star className="w-4 h-4 text-gold-500 fill-gold-500" />
            <span className="text-xs text-gold-600 font-medium">综合评分</span>
          </div>
          <motion.span
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ delay: 0.5 }}
            className="text-4xl font-bold text-warm-700 font-display"
          >
            {topSchool.overallScore}
          </motion.span>
          <span className="text-xs text-neutral-400">/ 100</span>
        </div>
      </div>
    </motion.div>
  );
}
