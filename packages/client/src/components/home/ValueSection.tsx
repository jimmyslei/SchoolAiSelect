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
    <section
      id="workflow"
      className="relative px-4 py-20 sm:px-6 lg:px-8 lg:py-28"
    >
      {/* Decorative dot grid background */}
      <div className="dot-grid pointer-events-none absolute inset-0 -z-10 opacity-40" />

      <div className="mx-auto grid max-w-6xl gap-10 lg:grid-cols-[1.05fr_0.95fr] lg:items-start">
        <motion.div
          initial={{ opacity: 0, y: 24 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, margin: "-100px" }}
          transition={{ duration: 0.6 }}
          className="relative overflow-hidden rounded-[32px] border border-border/40 bg-gradient-to-br from-warm-50/80 via-peach-50/40 to-sand-50/60 p-8 backdrop-blur-sm sm:p-10"
        >
          {/* Large decorative letter */}
          <div className="pointer-events-none absolute -right-4 -top-6 font-display text-[10rem] font-extrabold leading-none text-warm-200/40 select-none">
            W
          </div>

          <div className="relative">
            <div className="inline-flex items-center gap-2 rounded-full border border-warm-200/60 bg-background/80 px-4 py-2 text-xs font-medium text-warm-700 backdrop-blur-sm">
              <Sparkles className="h-3.5 w-3.5" />
              数据驱动的决策支持
            </div>
            <h2 className="mt-5 font-display text-3xl font-semibold tracking-tight text-neutral-900 sm:text-4xl lg:text-[2.75rem] lg:leading-[1.15]">
              让信息层级更自然，
              <span className="text-warm-600">
                把重点内容留在最该被看到的位置
              </span>
            </h2>
            <p className="mt-5 max-w-xl text-base leading-8 text-neutral-500">
              新布局将首页作为引导入口，把流程说明、价值说明与结果展示拆分为不同职责区域，减少拥挤感，同时保留原有搜索与分析逻辑。
            </p>
          </div>
        </motion.div>

        <div id="about" className="space-y-5">
          {values.map((item, index) => (
            <motion.div
              key={item.title}
              initial={{ opacity: 0, x: 24 }}
              whileInView={{ opacity: 1, x: 0 }}
              viewport={{ once: true, margin: "-80px" }}
              transition={{ duration: 0.45, delay: index * 0.1 }}
              className="group relative overflow-hidden rounded-[28px] border border-border/40 bg-background/60 p-6 backdrop-blur-sm transition-all duration-300 hover:-translate-y-0.5 hover:border-warm-200/60 hover:shadow-md"
            >
              <div className="flex items-start gap-4">
                <div className="flex h-13 w-13 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-sand-100 to-warm-100 text-warm-700 shadow-sm">
                  <item.icon className="h-5.5 w-5.5" />
                </div>
                <div>
                  <h3 className="text-lg font-semibold text-neutral-900">
                    {item.title}
                  </h3>
                  <p className="mt-2 text-sm leading-7 text-neutral-500">
                    {item.description}
                  </p>
                </div>
              </div>
              {/* Hover accent line */}
              <div className="absolute bottom-0 left-0 h-0.5 w-0 bg-gradient-to-r from-warm-400 to-peach-400 transition-all duration-300 group-hover:w-full" />
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  );
}
