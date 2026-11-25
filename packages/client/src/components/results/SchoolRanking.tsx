import { useCallback, useEffect, useMemo, useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { MapPin, RotateCcw, BarChart3, X } from "lucide-react";
import type { SchoolRanking, SchoolType } from "@/types/school";
import { SchoolTypeLabels } from "@/types/school";
import { RecommendBanner } from "./RecommendBanner";
import { SchoolCard } from "./SchoolCard";
import { SchoolDetail } from "./SchoolDetail";
import { CompareView } from "./CompareView";
import { Button } from "@/components/ui/button";
import { WeightAdjuster } from "@/components/search/WeightAdjuster";
import { useWeightConfig } from "@/hooks/useWeightConfig";
import { rerankWithWeights } from "@/lib/scoreCalculator";
import { cn } from "@/lib/utils";

interface SchoolRankingListProps {
  rankings: SchoolRanking[];
  schoolType: SchoolType;
  locationAddress: string;
  onReset: () => void;
  analyzedCount?: number;
  totalCount?: number;
}

export function SchoolRankingList({
  rankings,
  schoolType,
  locationAddress,
  onReset,
  analyzedCount = 0,
  totalCount = 0,
}: SchoolRankingListProps) {
  const [selectedSchool, setSelectedSchool] = useState<SchoolRanking | null>(
    null,
  );
  const [compareMode, setCompareMode] = useState(false);
  const [comparedIds, setComparedIds] = useState<Set<string>>(new Set());
  const [showCompare, setShowCompare] = useState(false);

  const { weights, setWeights, resetToDefault } = useWeightConfig();

  const isStreaming =
    totalCount > 0 &&
    analyzedCount < totalCount &&
    rankings.some((r) => r.analyzing);

  // 权重变化时重新排序（仅在分析完成后允许调整权重）
  const displayRankings = useMemo(() => {
    if (rankings.length === 0 || isStreaming) return rankings;
    // 检查权重是否接近默认值
    const isDefault = Object.keys(weights).every((k) => {
      const key = k as keyof typeof weights;
      return Math.abs(weights[key] - 0) < 0.01 || true; // always rerank
    });
    return rerankWithWeights(rankings, weights);
  }, [rankings, weights, isStreaming]);

  const topSchool = displayRankings.find((r) => r.isTopPick);

  const toggleCompare = useCallback((id: string) => {
    setComparedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else if (next.size < 4) {
        // 最多 4 所对比
        next.add(id);
      }
      return next;
    });
  }, []);

  // 对比学校列表
  const comparedRankings = useMemo(
    () => displayRankings.filter((r) => comparedIds.has(r.school.id)),
    [displayRankings, comparedIds],
  );

  // 进入对比视图
  if (showCompare && comparedRankings.length >= 2) {
    return (
      <CompareView
        rankings={comparedRankings}
        onBack={() => setShowCompare(false)}
      />
    );
  }

  if (selectedSchool) {
    return (
      <SchoolDetail
        ranking={selectedSchool}
        onBack={() => setSelectedSchool(null)}
      />
    );
  }

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      className="max-w-2xl mx-auto py-6 px-4"
    >
      {/* 顶部信息 */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h2 className="text-2xl font-bold text-neutral-800 font-display">
            {SchoolTypeLabels[schoolType]}推荐排名
          </h2>
          <div className="flex items-center gap-1.5 mt-1 text-sm text-neutral-400">
            <MapPin className="w-3.5 h-3.5" />
            <span>{locationAddress}</span>
            <span className="text-neutral-300">·</span>
            <span>
              {isStreaming
                ? `已分析 ${analyzedCount}/${totalCount} 所`
                : `共找到 ${rankings.length} 所学校`}
            </span>
          </div>
        </div>
        <Button
          onClick={onReset}
          variant="ghost"
          size="sm"
          aria-label="重新搜索"
          className="text-neutral-500 hover:text-warm-600 hover:bg-warm-50"
        >
          <RotateCcw className="w-4 h-4" />
          <span className="hidden sm:inline">重新搜索</span>
        </Button>
      </div>

      {/* 推荐横幅 */}
      {topSchool && <RecommendBanner topSchool={topSchool} />}

      {/* 权重调整 */}
      {!isStreaming && displayRankings.length > 0 && (
        <div className="mt-4">
          <WeightAdjuster
            weights={weights}
            onChange={setWeights}
            onReset={resetToDefault}
          />
        </div>
      )}

      {/* 对比模式切换 */}
      {!isStreaming && displayRankings.length >= 2 && (
        <div className="mt-3 flex justify-end">
          <Button
            variant={compareMode ? "warm" : "ghost"}
            size="sm"
            onClick={() => {
              setCompareMode(!compareMode);
              if (compareMode) {
                setComparedIds(new Set());
              }
            }}
            className={cn(
              "text-xs",
              !compareMode &&
                "text-neutral-500 hover:text-warm-600 hover:bg-warm-50",
            )}
          >
            <BarChart3 className="w-3.5 h-3.5" />
            {compareMode ? "退出对比" : "学校对比"}
          </Button>
        </div>
      )}

      {/* 排名列表 */}
      {displayRankings.length === 0 ? (
        <div className="mt-12 flex flex-col items-center gap-3 text-center py-12">
          <div className="w-16 h-16 rounded-full bg-neutral-100 flex items-center justify-center">
            <MapPin className="w-7 h-7 text-neutral-300" />
          </div>
          <p className="text-neutral-500 font-medium">
            附近暂未找到符合条件的学校
          </p>
          <p className="text-sm text-neutral-400">
            可以尝试更换地址或学校类型重新搜索
          </p>
        </div>
      ) : (
        <div className="mt-6 space-y-3">
          {displayRankings.map((ranking, index) => (
            <SchoolCard
              key={ranking.school.id}
              ranking={ranking}
              onClick={() => setSelectedSchool(ranking)}
              index={index}
              compareMode={compareMode && !ranking.analyzing}
              isSelected={comparedIds.has(ranking.school.id)}
              onToggleCompare={() => toggleCompare(ranking.school.id)}
            />
          ))}
        </div>
      )}

      {/* 底部说明 */}
      <div className="mt-8 text-center text-xs text-neutral-400 pb-8">
        <p>排名基于AI多维度综合分析，仅供参考</p>
        <p className="mt-1">数据来源：微信公众号、小红书、教育官网等公开信息</p>
      </div>

      {/* 浮动对比栏 */}
      <AnimatePresence>
        {compareMode && comparedIds.size > 0 && (
          <motion.div
            initial={{ y: 80, opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            exit={{ y: 80, opacity: 0 }}
            transition={{ type: "spring", stiffness: 400, damping: 30 }}
            className="fixed bottom-6 left-1/2 -translate-x-1/2 z-50"
          >
            <div className="flex items-center gap-3 rounded-2xl border border-border/40 bg-gradient-to-r from-card to-warm-50/30 shadow-xl backdrop-blur-sm px-5 py-3">
              <div className="flex items-center gap-2">
                <BarChart3 className="w-4 h-4 text-warm-500" />
                <span className="text-sm font-medium text-neutral-700">
                  已选 {comparedIds.size} 所
                </span>
              </div>
              <div className="flex items-center gap-1.5">
                {comparedRankings.slice(0, 3).map((r) => (
                  <span
                    key={r.school.id}
                    className="px-2 py-0.5 rounded-md text-[11px] font-medium bg-warm-50 text-warm-700 max-w-[100px] truncate"
                  >
                    {r.school.name}
                  </span>
                ))}
                {comparedIds.size > 3 && (
                  <span className="text-[11px] text-neutral-400">
                    +{comparedIds.size - 3}
                  </span>
                )}
              </div>
              <Button
                variant="warm"
                size="sm"
                disabled={comparedIds.size < 2}
                onClick={() => setShowCompare(true)}
                className="text-xs"
              >
                开始对比
              </Button>
              <button
                onClick={() => {
                  setComparedIds(new Set());
                }}
                className="p-1 rounded-full hover:bg-neutral-100 text-neutral-400 hover:text-neutral-600 transition-colors"
                aria-label="清除选择"
              >
                <X className="w-3.5 h-3.5" />
              </button>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  );
}
