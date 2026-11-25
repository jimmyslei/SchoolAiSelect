import { motion } from "framer-motion";
import { ArrowLeft, BarChart3 } from "lucide-react";
import type { SchoolRanking } from "@/types/school";
import { Button } from "@/components/ui/button";
import { ComparisonRadar } from "./ComparisonRadar";
import { ComparisonTable } from "./ComparisonTable";

interface CompareViewProps {
  rankings: SchoolRanking[];
  onBack: () => void;
}

export function CompareView({ rankings, onBack }: CompareViewProps) {
  return (
    <motion.div
      initial={{ opacity: 0, x: 30 }}
      animate={{ opacity: 1, x: 0 }}
      exit={{ opacity: 0, x: -30 }}
      className="max-w-4xl mx-auto py-6 px-4"
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

      {/* 标题 */}
      <div className="flex items-center gap-3 mb-6">
        <div className="w-10 h-10 rounded-xl bg-warm-100 flex items-center justify-center">
          <BarChart3 className="w-5 h-5 text-warm-600" />
        </div>
        <div>
          <h2 className="text-xl font-bold text-neutral-800 font-display">
            学校对比
          </h2>
          <p className="text-sm text-neutral-400">
            {rankings.map((r) => r.school.name).join(" vs ")}
          </p>
        </div>
      </div>

      {/* 雷达图 */}
      <div className="rounded-2xl border border-border/40 bg-gradient-to-br from-card to-warm-50/20 p-5 shadow-sm backdrop-blur-sm mb-5">
        <ComparisonRadar rankings={rankings} />
      </div>

      {/* 对比表格 */}
      <div className="rounded-2xl border border-border/40 bg-gradient-to-br from-card to-sand-50/20 p-5 shadow-sm backdrop-blur-sm">
        <ComparisonTable rankings={rankings} />
      </div>

      {/* 底部说明 */}
      <div className="mt-6 text-center text-xs text-neutral-400">
        <p>绿色高亮为该维度最优项，排名基于 AI 多维度综合分析</p>
      </div>
    </motion.div>
  );
}
