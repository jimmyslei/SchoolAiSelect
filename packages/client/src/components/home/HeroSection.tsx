import { motion } from "framer-motion";
import {
  ArrowRight,
  Compass,
  GraduationCap,
  MapPin,
  Sparkles,
  Stars,
  Trophy,
} from "lucide-react";
import { Button } from "@/components/ui/button";

interface HeroSectionProps {
  onStart: () => void;
  onStartUniversity?: () => void;
  onStartHighSchoolRecommend?: () => void;
}

const metrics = [
  { label: "分析维度", value: "7 项" },
  { label: "数据来源", value: "多平台" },
  { label: "决策方式", value: "AI + 公开信息" },
];

export function HeroSection({
  onStart,
  onStartUniversity,
  onStartHighSchoolRecommend,
}: HeroSectionProps) {
  return (
    <section
      id="top"
      className="relative overflow-hidden px-4 pb-20 pt-28 sm:px-6 sm:pb-24 lg:px-8"
    >
      <div className="absolute inset-0 -z-10">
        <div className="absolute inset-x-0 top-0 h-[600px] bg-linear-to-b from-peach-100/70 via-warm-50 to-transparent" />
        <div className="absolute left-[-8%] top-24 h-80 w-80 rounded-full bg-peach-200/50 blur-[80px]" />
        <div className="absolute right-[-4%] top-12 h-96 w-96 rounded-full bg-gold-100/60 blur-[90px]" />
        <div className="absolute bottom-0 left-1/2 h-72 w-[75%] -translate-x-1/2 rounded-full bg-sand-200/40 blur-[80px]" />
        {/* Animated floating shapes */}
        <div className="animate-float-soft absolute left-[12%] top-[40%] h-3 w-3 rounded-full bg-warm-400/30" />
        <div
          className="animate-float-soft absolute right-[18%] top-[30%] h-2 w-2 rounded-full bg-peach-400/25"
          style={{ animationDelay: "1.5s" }}
        />
        <div
          className="animate-float-soft absolute left-[65%] top-[55%] h-4 w-4 rounded-full bg-gold-300/20"
          style={{ animationDelay: "3s" }}
        />
      </div>

      <div className="mx-auto grid max-w-6xl gap-10 lg:grid-cols-[minmax(0,1.05fr)_minmax(320px,0.95fr)] lg:items-center">
        <motion.div
          initial={{ opacity: 0, y: 24 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.7, ease: "easeOut" }}
          className="max-w-2xl"
        >
          <div className="mb-7 inline-flex items-center gap-2.5 rounded-full border border-warm-200/70 bg-background/80 px-4 py-2 text-xs font-medium text-warm-700 shadow-xs backdrop-blur-sm">
            <span className="relative flex h-2 w-2">
              <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-warm-400 opacity-75" />
              <span className="relative inline-flex h-2 w-2 rounded-full bg-warm-500" />
            </span>
            AI 驱动的智能择校 · 大学报考分析
          </div>

          <div className="space-y-6">
            <div className="space-y-4">
              <p className="text-sm font-medium uppercase tracking-[0.28em] text-warm-600">
                School &amp; University Match
              </p>
              <h1 className="text-balance font-display text-4xl font-bold leading-[1.1] tracking-tight text-neutral-900 sm:text-5xl lg:text-[3.5rem]">
                从家庭位置出发，
                <span className="block bg-linear-to-r from-warm-600 via-peach-500 to-gold-400 bg-clip-text text-transparent">
                  找到更适合的学校与大学
                </span>
              </h1>
              <p className="max-w-xl text-base leading-8 text-neutral-500 sm:text-lg">
                自动定位、整合公开教育信息、生成多维评分与推荐理由。支持 K-12
                学校搜索与高考大学报考分析，让择校更清晰、更安心。
              </p>
            </div>

            <div
              id="hero-start"
              className="flex flex-col gap-3 sm:flex-row sm:items-center"
            >
              <Button
                onClick={onStart}
                variant="warm"
                size="xl"
                className="group"
              >
                开始寻找学校
                <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5" />
              </Button>
              {onStartUniversity && (
                <Button
                  onClick={onStartUniversity}
                  variant="outline"
                  size="xl"
                  className="group border-purple-200 text-purple-700 hover:bg-purple-50 hover:text-purple-800"
                >
                  <GraduationCap className="h-4 w-4" />
                  大学报考分析
                </Button>
              )}
              {onStartHighSchoolRecommend && (
                <Button
                  onClick={onStartHighSchoolRecommend}
                  variant="outline"
                  size="xl"
                  className="group border-green-200 text-green-700 hover:bg-green-50 hover:text-green-800"
                >
                  <GraduationCap className="h-4 w-4" />
                  高中择校推荐
                </Button>
              )}
              {/* <a
                href="#workflow"
                className="inline-flex h-14 items-center justify-center rounded-full border border-border/80 bg-background/70 px-6 text-sm font-medium text-neutral-700 transition-colors hover:bg-accent hover:text-warm-700"
              >
                查看流程说明
              </a> */}
            </div>

            <div className="grid gap-3 pt-6 sm:grid-cols-3">
              {metrics.map((item, idx) => (
                <motion.div
                  key={item.label}
                  initial={{ opacity: 0, y: 12 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ duration: 0.4, delay: 0.6 + idx * 0.1 }}
                  className="group rounded-[20px] border border-border/50 bg-background/60 px-4 py-4 backdrop-blur-sm transition-all duration-200 hover:border-warm-200/70 hover:shadow-sm"
                >
                  <div className="tabular-nums text-2xl font-semibold text-neutral-900">
                    {item.value}
                  </div>
                  <div className="mt-1 text-xs text-neutral-400">
                    {item.label}
                  </div>
                </motion.div>
              ))}
            </div>
          </div>
        </motion.div>

        <motion.div
          initial={{ opacity: 0, scale: 0.96, y: 24 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          transition={{ duration: 0.8, delay: 0.1, ease: "easeOut" }}
          className="relative"
        >
          <div className="relative overflow-hidden rounded-[36px] border border-border/40 bg-gradient-to-br from-background/95 via-warm-50/30 to-peach-50/20 p-5 shadow-lg backdrop-blur-sm sm:p-6">
            {/* Grid pattern overlay */}
            <div className="warm-grid pointer-events-none absolute inset-0 opacity-60" />
            <div className="absolute right-5 top-5 rounded-full border border-warm-200/50 bg-background/80 px-3 py-1 text-xs font-medium text-warm-700 backdrop-blur-sm">
              实时生成推荐结果
            </div>

            <div className="relative rounded-[28px] border border-border/30 bg-gradient-to-br from-background/90 to-warm-50/40 p-5 backdrop-blur-sm sm:p-6">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <p className="text-sm font-medium text-neutral-500">
                    推荐概览
                  </p>
                  <h2 className="mt-2 font-display text-2xl font-semibold text-neutral-900">
                    海淀区 · 小学推荐
                  </h2>
                </div>
                <div className="rounded-2xl bg-warm-100 px-3 py-2 text-right">
                  <div className="text-xs text-warm-700">综合评分</div>
                  <div className="text-2xl font-semibold text-warm-700">92</div>
                </div>
              </div>

              <div className="mt-6 space-y-3">
                <div className="rounded-[24px] border border-gold-200/70 bg-linear-to-r from-gold-50 to-peach-50 p-4">
                  <div className="flex items-center gap-3">
                    <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-gold-300 text-white">
                      <Trophy className="h-5 w-5" />
                    </div>
                    <div>
                      <p className="text-xs font-medium uppercase tracking-[0.2em] text-gold-500">
                        Top Pick
                      </p>
                      <h3 className="mt-1 text-lg font-semibold text-neutral-900">
                        中关村第三小学
                      </h3>
                    </div>
                  </div>
                  <p className="mt-3 text-sm leading-7 text-neutral-600">
                    师资稳定、家长口碑突出、公开内容活跃，适合作为优先关注对象。
                  </p>
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="rounded-[24px] border border-border/70 bg-background/80 p-4">
                    <div className="flex items-center gap-2 text-sm font-medium text-neutral-700">
                      <MapPin className="h-4 w-4 text-warm-500" />
                      距离与位置
                    </div>
                    <p className="mt-3 text-sm leading-7 text-neutral-600">
                      自动定位家庭地址，优先展示通勤更友好的学校。
                    </p>
                  </div>
                  <div className="rounded-[24px] border border-border/70 bg-background/80 p-4">
                    <div className="flex items-center gap-2 text-sm font-medium text-neutral-700">
                      <Compass className="h-4 w-4 text-warm-500" />
                      多维分析
                    </div>
                    <p className="mt-3 text-sm leading-7 text-neutral-600">
                      师资、口碑、活动、获奖与内容质量统一纳入评分。
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <motion.div
            animate={{ y: [0, -10, 0] }}
            transition={{ duration: 6, repeat: Infinity, ease: "easeInOut" }}
            className="absolute -left-3 top-10 hidden rounded-full border border-border/70 bg-background/85 px-4 py-2 text-xs font-medium text-neutral-700 shadow-sm backdrop-blur-sm sm:flex sm:items-center sm:gap-2"
          >
            <Stars className="h-3.5 w-3.5 text-gold-500" />
            重点学校优先突出
          </motion.div>
        </motion.div>
      </div>
    </section>
  );
}
