import { useState } from "react";
import { motion } from "framer-motion";
import { MapPin, RotateCcw } from "lucide-react";
import type { SchoolRanking, SchoolType } from "@/types/school";
import { SchoolTypeLabels } from "@/types/school";
import { RecommendBanner } from "./RecommendBanner";
import { SchoolCard } from "./SchoolCard";
import { SchoolDetail } from "./SchoolDetail";
import { Button } from "@/components/ui/button";

interface SchoolRankingListProps {
  rankings: SchoolRanking[];
  schoolType: SchoolType;
  locationAddress: string;
  onReset: () => void;
}

export function SchoolRankingList({
  rankings,
  schoolType,
  locationAddress,
  onReset,
}: SchoolRankingListProps) {
  const [selectedSchool, setSelectedSchool] = useState<SchoolRanking | null>(
    null,
  );

  const topSchool = rankings.find((r) => r.isTopPick);

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
            <span>共找到 {rankings.length} 所学校</span>
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

      {/* 排名列表 */}
      {rankings.length === 0 ? (
        <div className="mt-12 flex flex-col items-center gap-3 text-center py-12">
          <div className="w-16 h-16 rounded-full bg-neutral-100 flex items-center justify-center">
            <MapPin className="w-7 h-7 text-neutral-300" />
          </div>
          <p className="text-neutral-500 font-medium">附近暂未找到符合条件的学校</p>
          <p className="text-sm text-neutral-400">可以尝试更换地址或学校类型重新搜索</p>
        </div>
      ) : (
        <div className="mt-6 space-y-3">
          {rankings.map((ranking, index) => (
            <SchoolCard
              key={ranking.school.id}
              ranking={ranking}
              onClick={() => setSelectedSchool(ranking)}
              index={index}
            />
          ))}
        </div>
      )}

      {/* 底部说明 */}
      <div className="mt-8 text-center text-xs text-neutral-400 pb-8">
        <p>排名基于AI多维度综合分析，仅供参考</p>
        <p className="mt-1">数据来源：微信公众号、小红书、教育官网等公开信息</p>
      </div>
    </motion.div>
  );
}
