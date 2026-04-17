import { GraduationCap, Heart, MapPinned, Sparkles } from "lucide-react";

const footerColumns = [
  {
    title: "核心能力",
    items: [
      "定位周边学校",
      "AI 综合评分",
      "多来源信息整合",
      "结果对比与详情查看",
    ],
  },
  {
    title: "数据来源",
    items: ["微信公众号", "小红书", "教育局官网", "公开教育资讯站点"],
  },
];

export function Footer() {
  return (
    <footer className="px-4 pb-6 pt-16 sm:px-6 lg:px-8">
      <div className="surface-elevated mx-auto max-w-6xl rounded-[32px] px-6 py-10 sm:px-10">
        <div className="grid gap-10 lg:grid-cols-[1.2fr_0.8fr_0.8fr]">
          <div className="space-y-5">
            <div className="flex items-center gap-3">
              <div className="flex h-12 w-12 items-center justify-center rounded-full bg-linear-to-br from-warm-500 to-peach-400 text-white shadow-[0_14px_28px_rgba(224,122,63,0.24)]">
                <GraduationCap className="h-5 w-5" />
              </div>
              <div>
                <p className="text-[11px] uppercase tracking-[0.24em] text-warm-600">
                  Warm Decision Support
                </p>
                <h2 className="font-display text-xl font-semibold text-neutral-900">
                  学校推荐系统
                </h2>
              </div>
            </div>

            <p className="max-w-md text-sm leading-7 text-neutral-600">
              以位置为起点，以公开数据与 AI
              分析为依据，帮助家长快速筛选更适合孩子的学校选择。
            </p>

            <div className="flex flex-wrap gap-3 text-xs text-neutral-600">
              <span className="inline-flex items-center gap-2 rounded-full bg-warm-100 px-3 py-1.5">
                <Sparkles className="h-3.5 w-3.5 text-warm-600" />
                暖色主题体验
              </span>
              <span className="inline-flex items-center gap-2 rounded-full bg-sand-100 px-3 py-1.5">
                <MapPinned className="h-3.5 w-3.5 text-warm-700" />
                响应式多端适配
              </span>
            </div>
          </div>

          {footerColumns.map((column) => (
            <div key={column.title}>
              <h3 className="mb-4 text-sm font-semibold text-neutral-900">
                {column.title}
              </h3>
              <ul className="space-y-3 text-sm text-neutral-600">
                {column.items.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </div>
          ))}
        </div>

        <div className="mt-10 flex flex-col gap-3 border-t border-border/80 pt-6 text-xs text-neutral-500 sm:flex-row sm:items-center sm:justify-between">
          <p>© 2026 学校推荐系统 · 基于公开信息的辅助决策工具</p>
          <p className="inline-flex items-center gap-1.5">
            Made with{" "}
            <Heart className="h-3.5 w-3.5 fill-warm-400 text-warm-400" /> for
            parents
          </p>
        </div>
      </div>
    </footer>
  );
}
