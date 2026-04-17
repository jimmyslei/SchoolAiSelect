import { motion } from "framer-motion";
import { BrainCircuit, ChartColumnIncreasing, MapPinned } from "lucide-react";

const features = [
  {
    icon: MapPinned,
    eyebrow: "Step 01",
    title: "从家庭位置快速开始",
    description:
      "自动获取当前位置，也支持手动输入地址，确保不同场景下都能顺畅进入择校流程。",
  },
  {
    icon: BrainCircuit,
    eyebrow: "Step 02",
    title: "整合公开信息并生成分析",
    description:
      "系统从多个公开渠道汇总学校信息，结合 AI 生成更易理解的推荐理由与摘要。",
  },
  {
    icon: ChartColumnIncreasing,
    eyebrow: "Step 03",
    title: "用统一评分帮助比较",
    description:
      "通过多维度评分、榜单排序与图表对比，帮助家长更快锁定重点学校。",
  },
];

export function FeaturesSection() {
  return (
    <section id="features" className="px-4 py-16 sm:px-6 lg:px-8 lg:py-20">
      <div className="mx-auto max-w-6xl">
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, margin: "-100px" }}
          transition={{ duration: 0.6 }}
          className="grid gap-6 border-b border-border/80 pb-10 lg:grid-cols-[0.9fr_1.1fr] lg:items-end"
        >
          <div>
            <p className="text-sm font-medium uppercase tracking-[0.24em] text-warm-600">
              Product Highlights
            </p>
            <h2 className="mt-3 font-display text-3xl font-semibold text-neutral-900 sm:text-4xl">
              用更清晰的结构，
              <span className="block">把择校流程拆成可理解的三步。</span>
            </h2>
          </div>
          <p className="max-w-2xl text-sm leading-7 text-neutral-600 sm:text-base">
            页面不再堆叠零散信息，而是围绕“开始、分析、比较”三个动作组织内容，让用户在首屏就理解产品价值与下一步操作。
          </p>
        </motion.div>

        <div className="mt-10 grid gap-6 lg:grid-cols-3">
          {features.map((feature, index) => (
            <motion.div
              key={feature.title}
              initial={{ opacity: 0, y: 24 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true, margin: "-80px" }}
              transition={{ duration: 0.5, delay: index * 0.12 }}
              className="surface-panel rounded-[30px] p-6"
            >
              <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-warm-100 text-warm-600">
                <feature.icon className="h-5 w-5" />
              </div>
              <p className="mt-6 text-xs font-medium uppercase tracking-[0.24em] text-warm-600">
                {feature.eyebrow}
              </p>
              <h3 className="mt-3 text-xl font-semibold text-neutral-900">
                {feature.title}
              </h3>
              <p className="mt-3 text-sm leading-7 text-neutral-600">
                {feature.description}
              </p>
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  );
}
