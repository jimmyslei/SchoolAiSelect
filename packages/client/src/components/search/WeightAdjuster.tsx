import { useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { SlidersHorizontal, RotateCcw, ChevronDown, Check } from "lucide-react";
import type { WeightConfig } from "@/lib/scoreCalculator";
import {
  WEIGHT_DIMENSIONS,
  WEIGHT_PRESETS,
  normalizeWeights,
  DEFAULT_WEIGHTS,
} from "@/lib/scoreCalculator";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";

interface WeightAdjusterProps {
  weights: WeightConfig;
  onChange: (weights: WeightConfig) => void;
  onReset: () => void;
}

export function WeightAdjuster({
  weights,
  onChange,
  onReset,
}: WeightAdjusterProps) {
  const [expanded, setExpanded] = useState(false);

  const handleSliderChange = (key: keyof WeightConfig, value: number) => {
    onChange(normalizeWeights({ ...weights, [key]: value / 100 }));
  };

  const handlePreset = (presetKey: string) => {
    const preset = WEIGHT_PRESETS[presetKey];
    if (preset) onChange(preset);
  };

  const activePreset = Object.entries(WEIGHT_PRESETS).find(([, preset]) =>
    Object.keys(DEFAULT_WEIGHTS).every(
      (k) =>
        Math.abs(
          (preset[k as keyof WeightConfig] ?? 0) -
            (weights[k as keyof WeightConfig] ?? 0),
        ) < 0.01,
    ),
  )?.[0];

  const totalWeight = Object.values(weights).reduce((s, w) => s + w, 0);

  return (
    <div className="bg-card rounded-2xl border border-border/60 shadow-sm dark:shadow-[0_8px_24px_rgba(0,0,0,0.35)] overflow-hidden">
      {/* 触发器 */}
      <button
        onClick={() => setExpanded(!expanded)}
        className="w-full flex items-center justify-between px-5 py-3.5 hover:bg-warm-50/50 transition-colors"
      >
        <div className="flex items-center gap-2.5">
          <SlidersHorizontal className="w-4 h-4 text-warm-500" />
          <span className="text-sm font-medium text-neutral-700">
            调整排名权重
          </span>
          {activePreset && activePreset !== "balanced" && (
            <span className="px-2 py-0.5 rounded-md text-[11px] font-medium bg-warm-50 text-warm-600">
              {activePreset === "balanced" ? "均衡" : activePreset}
            </span>
          )}
        </div>
        <ChevronDown
          className={cn(
            "w-4 h-4 text-neutral-400 transition-transform duration-200",
            expanded && "rotate-180",
          )}
        />
      </button>

      {/* 展开面板 */}
      <AnimatePresence>
        {expanded && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: "auto", opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.25 }}
            className="overflow-hidden"
          >
            <div className="px-5 pb-5 pt-1 border-t border-neutral-50">
              {/* 预设模板 */}
              <div className="flex flex-wrap gap-2 mb-5">
                {Object.entries(WEIGHT_PRESETS).map(([key, preset]) => (
                  <button
                    key={key}
                    onClick={() => handlePreset(key)}
                    className={cn(
                      "px-3 py-1.5 rounded-lg text-xs font-medium transition-all",
                      activePreset === key
                        ? "bg-warm-500 text-white shadow-sm"
                        : "bg-neutral-50 text-neutral-600 hover:bg-warm-50 hover:text-warm-700",
                    )}
                  >
                    {key === "balanced" ? "均衡型" : key}
                  </button>
                ))}
              </div>

              {/* 滑块列表 */}
              <div className="space-y-3.5">
                {WEIGHT_DIMENSIONS.map((dim) => {
                  const value = weights[dim.key];
                  const percent = Math.round(value * 100);
                  return (
                    <div key={dim.key} className="group">
                      <div className="flex items-center justify-between mb-1">
                        <span className="text-xs font-medium text-neutral-600">
                          {dim.label}
                        </span>
                        <span
                          className="text-xs font-semibold tabular-nums"
                          style={{ color: dim.color }}
                        >
                          {percent}%
                        </span>
                      </div>
                      <div className="relative h-2 bg-neutral-100 rounded-full overflow-hidden">
                        <div
                          className="absolute inset-y-0 left-0 rounded-full transition-all duration-150"
                          style={{
                            width: `${percent}%`,
                            backgroundColor: dim.color,
                            opacity: 0.25,
                          }}
                        />
                        <input
                          type="range"
                          min={0}
                          max={100}
                          step={1}
                          value={percent}
                          onChange={(e) =>
                            handleSliderChange(dim.key, +e.target.value)
                          }
                          className="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
                          style={{ margin: 0 }}
                        />
                        {/* 拖拽手柄 */}
                        <div
                          className="absolute top-1/2 -translate-y-1/2 w-3.5 h-3.5 rounded-full bg-white shadow-sm border-2 pointer-events-none transition-all duration-150"
                          style={{
                            left: `calc(${percent}% - 7px)`,
                            borderColor: dim.color,
                          }}
                        />
                      </div>
                    </div>
                  );
                })}
              </div>

              {/* 底部操作 */}
              <div className="flex items-center justify-between mt-5 pt-3 border-t border-border/50">
                <span className="text-[11px] text-neutral-400">
                  权重总和：{Math.round(totalWeight * 100)}%
                  {Math.abs(totalWeight - 1) > 0.01 && (
                    <span className="text-warm-500 ml-1">（已自动归一化）</span>
                  )}
                </span>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={onReset}
                  className="text-xs text-neutral-500 hover:text-warm-600"
                >
                  <RotateCcw className="w-3 h-3" />
                  恢复默认
                </Button>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
