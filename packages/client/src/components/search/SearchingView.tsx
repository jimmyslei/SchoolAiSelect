import { motion } from "framer-motion";
import { BarChart3, Brain, Search, Sparkles } from "lucide-react";
import { useEffect, useState } from "react";
import { SchoolTypeLabels } from "@/types/school";
import type { SchoolType } from "@/types/school";

interface SearchingViewProps {
  schoolType: SchoolType;
}

const steps = [
  { icon: Search, text: "搜索附近学校信息" },
  { icon: Brain, text: "调用 AI 生成分析摘要" },
  { icon: BarChart3, text: "计算多维度综合评分" },
  { icon: Sparkles, text: "整理推荐顺序与理由" },
];

export function SearchingView({ schoolType }: SearchingViewProps) {
  const [currentStep, setCurrentStep] = useState(0);

  useEffect(() => {
    const timer = setInterval(() => {
      setCurrentStep((prev) => (prev < steps.length - 1 ? prev + 1 : prev));
    }, 650);

    return () => clearInterval(timer);
  }, []);

  return (
    <motion.section
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      className="mx-auto max-w-3xl overflow-hidden rounded-[32px] border border-border/40 bg-gradient-to-br from-background via-warm-50/20 to-peach-50/20 p-6 shadow-lg backdrop-blur-sm sm:p-8"
    >
      <div className="grid gap-8 lg:grid-cols-[0.9fr_1.1fr] lg:items-center">
        <div>
          <div className="inline-flex items-center gap-2 rounded-full bg-warm-100 px-4 py-2 text-xs font-medium text-warm-700">
            <Sparkles className="h-3.5 w-3.5" />
            搜索与分析进行中
          </div>
          <h2 className="mt-5 font-display text-2xl font-semibold text-neutral-900 sm:text-3xl">
            正在生成 {SchoolTypeLabels[schoolType]} 推荐结果
          </h2>
          <p className="mt-3 text-sm leading-7 text-neutral-600">
            系统会依次完成学校搜索、内容分析、评分计算与推荐整理，完成后自动进入结果页。
          </p>
        </div>

        <div className="overflow-hidden rounded-[28px] border border-border/30 bg-gradient-to-br from-warm-50/40 to-peach-50/20 p-5 backdrop-blur-sm sm:p-6">
          <div className="flex flex-col items-center text-center">
            <div className="relative mb-6 flex h-24 w-24 items-center justify-center rounded-full bg-gradient-to-br from-warm-100 to-peach-100/60 shadow-sm">
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
              <Search className="relative h-8 w-8 text-warm-600" />
            </div>

            <div className="w-full space-y-3 text-left">
              {steps.map((step, index) => {
                const isActive = index === currentStep;
                const isCompleted = index < currentStep;

                return (
                  <motion.div
                    key={step.text}
                    initial={{ opacity: 0, x: -10 }}
                    animate={{ opacity: 1, x: 0 }}
                    transition={{ delay: index * 0.12 }}
                    className={
                      "flex items-center gap-3 rounded-2xl px-4 py-3 transition-colors " +
                      (isActive ? "bg-warm-50" : "bg-background/60")
                    }
                  >
                    <div
                      className={
                        "flex h-9 w-9 shrink-0 items-center justify-center rounded-xl " +
                        (isCompleted
                          ? "bg-sage-100 text-sage-500"
                          : isActive
                            ? "bg-warm-100 text-warm-600"
                            : "bg-muted text-neutral-400")
                      }
                    >
                      <step.icon className="h-4 w-4" />
                    </div>
                    <span
                      className={
                        "text-sm " +
                        (isCompleted
                          ? "text-sage-600"
                          : isActive
                            ? "font-medium text-warm-700"
                            : "text-neutral-500")
                      }
                    >
                      {step.text}
                    </span>
                    {isActive && (
                      <motion.div
                        animate={{ opacity: [0.3, 1, 0.3] }}
                        transition={{ duration: 1.1, repeat: Infinity }}
                        className="ml-auto h-2 w-2 rounded-full bg-warm-400"
                      />
                    )}
                  </motion.div>
                );
              })}
            </div>
          </div>
        </div>
      </div>
    </motion.section>
  );
}
