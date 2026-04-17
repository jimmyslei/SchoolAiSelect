import { motion } from "framer-motion";
import { Database, ShieldCheck, Sparkles, TrendingUp } from "lucide-react";

const values = [
  {
    icon: Database,
    title: "多来源公开信息整合",
    description:
      "汇总公众号、小红书、教育官网等公开内容，减少单一来源带来的信息偏差。",
  },
  {
    icon: ShieldCheck,
    title: "推荐理由可读可解释",
    description:
      "不仅给出分数，还会展示推荐摘要、维度表现与来源信息，帮助理解结果。",
  },
  {
    icon: TrendingUp,
    title: "比较视图更适合决策",
    description:
      "榜单、雷达图与柱状图共同呈现，让重点学校与差异项更容易被看见。",
  },
];

export function ValueSection() {
  return (
    <section id="workflow" className="px-4 py-16 sm:px-6 lg:px-8 lg:py-20">
      <div className="mx-auto grid max-w-6xl gap-8 lg:grid-cols-[0.95fr_1.05fr] lg:items-start">
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, margin: "-100px" }}
          transition={{ duration: 0.6 }}
          className="surface-elevated rounded-[32px] p-8 sm:p-10"
        >
          <div className="inline-flex items-center gap-2 rounded-full bg-warm-100 px-4 py-2 text-xs font-medium text-warm-700">
            <Sparkles className="h-3.5 w-3.5" />
            Warm visual system
          </div>
          <h2 className="mt-5 font-display text-3xl font-semibold text-neutral-900 sm:text-4xl">
            让信息层级更自然，
            <span className="block">把重点内容留在最该被看到的位置。</span>
          </h2>
          <p className="mt-5 max-w-xl text-sm leading-8 text-neutral-600 sm:text-base">
            新布局将首页作为引导入口，把流程说明、价值说明与结果展示拆分为不同职责区域，减少拥挤感，同时保留原有搜索与分析逻辑。
          </p>
        </motion.div>

        <div id="about" className="space-y-4">
          {values.map((item, index) => (
            <motion.div
              key={item.title}
              initial={{ opacity: 0, x: 24 }}
              whileInView={{ opacity: 1, x: 0 }}
              viewport={{ once: true, margin: "-80px" }}
              transition={{ duration: 0.45, delay: index * 0.1 }}
              className="surface-panel rounded-[28px] p-6"
            >
              <div className="flex items-start gap-4">
                <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-sand-100 text-warm-700">
                  <item.icon className="h-5 w-5" />
                </div>
                <div>
                  <h3 className="text-lg font-semibold text-neutral-900">
                    {item.title}
                  </h3>
                  <p className="mt-2 text-sm leading-7 text-neutral-600">
                    {item.description}
                  </p>
                </div>
              </div>
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  );
}
