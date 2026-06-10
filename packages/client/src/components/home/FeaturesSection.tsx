import { motion } from "framer-motion";
import {
  BrainCircuit,
  ChartColumnIncreasing,
  MapPinned,
  ArrowUpRight,
} from "lucide-react";

const features = [
  {
    icon: MapPinned,
    eyebrow: "Step 01",
    title: "从家庭位置快速开始",
    description:
      "自动获取当前位置，也支持手动输入地址，确保不同场景下都能顺畅进入择校流程。",
    accent: "from-warm-100/80 to-peach-50/60",
    iconBg: "bg-warm-200/80 text-warm-700",
    span: "lg:col-span-2",
    highlight: true,
  },
  {
    icon: BrainCircuit,
    eyebrow: "Step 02",
    title: "整合公开信息并生成分析",
    description:
      "系统从多个公开渠道汇总学校信息，结合 AI 生成更易理解的推荐理由与摘要。",
    accent: "from-sky-50/80 to-sage-50/60",
    iconBg: "bg-sky-100/80 text-sky-600",
    span: "lg:col-span-1 lg:row-span-2",
    highlight: false,
  },
  {
    icon: ChartColumnIncreasing,
    eyebrow: "Step 03",
    title: "用统一评分帮助比较",
    description:
      "通过多维度评分、榜单排序与图表对比，帮助家长更快锁定重点学校。",
    accent: "from-gold-50/80 to-sand-50/60",
    iconBg: "bg-gold-100/80 text-gold-600",
    span: "lg:col-span-1",
    highlight: false,
  },
];

export function FeaturesSection() {
  return (
    <section
      id="features"
      className="relative px-4 py-20 sm:px-6 lg:px-8 lg:py-28"
    >
      {/* Decorative background number */}
      <div className="pointer-events-none absolute right-8 top-8 font-display text-[12rem] font-extrabold leading-none text-warm-100/50 select-none sm:text-[16rem]">
        3
      </div>

      <div className="relative mx-auto max-w-6xl">
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, margin: "-100px" }}
          transition={{ duration: 0.6 }}
          className="mb-12 max-w-2xl"
        >
          <div className="inline-flex items-center gap-2.5 rounded-full border border-warm-200/60 bg-background/80 px-4 py-2 text-xs font-medium text-warm-700 backdrop-blur-sm">
            <span className="inline-block h-1.5 w-1.5 rounded-full bg-warm-500" />
            Product Highlights
          </div>
          <h2 className="mt-5 font-display text-3xl font-semibold tracking-tight text-neutral-900 sm:text-4xl lg:text-[2.75rem] lg:leading-[1.15]">
            用更清晰的结构，
            <span className="text-warm-600">把择校流程拆成三步</span>
          </h2>
          <p className="mt-4 max-w-xl text-base leading-8 text-neutral-500">
            围绕"开始、分析、比较"三个动作组织内容，让用户在首屏就理解产品价值。
          </p>
        </motion.div>

        <div className="grid gap-5 lg:grid-cols-3">
          {features.map((feature, index) => (
            <motion.div
              key={feature.title}
              initial={{ opacity: 0, y: 28 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true, margin: "-60px" }}
              transition={{ duration: 0.55, delay: index * 0.1 }}
              className={`group relative overflow-hidden rounded-[28px] border border-border/40 bg-gradient-to-br ${feature.accent} p-6 backdrop-blur-sm sm:p-8 ${feature.span} transition-all duration-300 hover:-translate-y-0.5 hover:shadow-lg`}
            >
              {feature.highlight && (
                <div className="absolute right-6 top-6 flex items-center gap-1.5 rounded-full bg-warm-500/10 px-3 py-1 text-xs font-medium text-warm-700 backdrop-blur-sm">
                  <ArrowUpRight className="h-3 w-3" />
                  推荐入口
                </div>
              )}

              <div
                className={`flex h-14 w-14 items-center justify-center rounded-2xl ${feature.iconBg} shadow-sm`}
              >
                <feature.icon className="h-6 w-6" />
              </div>

              <p className="mt-6 text-xs font-semibold uppercase tracking-[0.2em] text-warm-600">
                {feature.eyebrow}
              </p>
              <h3 className="mt-2 font-display text-xl font-semibold text-neutral-900 sm:text-2xl">
                {feature.title}
              </h3>
              <p className="mt-3 max-w-md text-sm leading-7 text-neutral-600 sm:text-base">
                {feature.description}
              </p>

              {/* Decorative corner glow */}
              <div className="absolute -bottom-10 -right-10 h-36 w-36 rounded-full bg-white/40 blur-2xl transition-transform duration-500 group-hover:scale-125" />
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  );
}
