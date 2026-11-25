/**
 * 图表统一取色：recharts 仅接受具体颜色值，无法直接读取 CSS 变量，
 * 因此这里按当前主题给出一套与 index.css 暗色变量对齐的色板。
 */
export function getChartTheme(isDark: boolean) {
  return {
    grid: isDark ? "rgba(210, 198, 183, 0.16)" : "#e8e6e1",
    gridSoft: isDark ? "rgba(210, 198, 183, 0.14)" : "#f0ede8",
    axisLabel: isDark ? "#b8a898" : "#7d7870",
    axisTick: isDark ? "#9a8a7c" : "#a8a49c",
    valueLabel: isDark ? "#d2c6b7" : "#5c574f",
    referenceLine: isDark ? "#8a7d72" : "#a3a3a3",
    referenceText: isDark ? "#b8a898" : "#737373",
    tooltip: {
      background: isDark ? "rgba(28, 24, 22, 0.96)" : "rgba(255, 255, 255, 0.95)",
      border: `1px solid ${isDark ? "rgba(210, 198, 183, 0.14)" : "#e8e6e1"}`,
      borderRadius: "12px",
      boxShadow: isDark
        ? "0 8px 24px rgba(0, 0, 0, 0.45)"
        : "0 4px 12px rgba(0, 0, 0, 0.06)",
      fontSize: "13px",
      color: isDark ? "#e8ddd2" : "#2d2621",
    },
  };
}
