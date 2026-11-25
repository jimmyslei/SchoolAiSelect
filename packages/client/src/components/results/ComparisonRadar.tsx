import {
  Radar,
  RadarChart as RechartsRadarChart,
  PolarGrid,
  PolarAngleAxis,
  PolarRadiusAxis,
  ResponsiveContainer,
  Tooltip,
  Legend,
} from "recharts";
import type { SchoolRanking } from "@/types/school";
import { useIsDark } from "@/hooks/useTheme";
import { getChartTheme } from "@/lib/chartTheme";

/** 雷达图配色 */
const RADAR_COLORS = [
  { stroke: "#ed8c42", fill: "rgba(237,140,66,0.15)" },
  { stroke: "#66b2a0", fill: "rgba(102,178,160,0.15)" },
  { stroke: "#6fa8d6", fill: "rgba(111,168,214,0.15)" },
  { stroke: "#d4a843", fill: "rgba(212,168,67,0.15)" },
];

interface ComparisonRadarProps {
  rankings: SchoolRanking[];
}

/** 多学校叠加雷达图 */
export function ComparisonRadar({ rankings }: ComparisonRadarProps) {
  const isDark = useIsDark();
  const chart = getChartTheme(isDark);

  const dimensionKeys = [
    { key: "teachingStaffScore", label: "师资力量" },
    { key: "reputationScore", label: "用户口碑" },
    { key: "contentQualityScore", label: "内容质量" },
    { key: "activityScore", label: "网络活跃" },
    { key: "studentActivityScore", label: "学生活动" },
    { key: "awardsScore", label: "获奖荣誉" },
  ];

  // 构建 Recharts 数据：每行一个维度，每所学校一列
  const data = dimensionKeys.map((dim) => {
    const row: Record<string, string | number> = { dimension: dim.label };
    rankings.forEach((r) => {
      if (dim.key === "awardsScore") {
        row[r.school.name] = Math.min(r.analysis.awardsCount * 5, 100);
      } else {
        row[r.school.name] = r.analysis[
          dim.key as keyof typeof r.analysis
        ] as number;
      }
    });
    return row;
  });

  return (
    <div className="w-full">
      <h4 className="text-sm font-medium text-neutral-600 mb-2 text-center">
        多维度对比雷达图
      </h4>
      <ResponsiveContainer width="100%" height={360}>
        <RechartsRadarChart data={data} cx="50%" cy="50%" outerRadius="70%">
          <PolarGrid stroke={chart.grid} strokeDasharray="3 3" />
          <PolarAngleAxis
            dataKey="dimension"
            tick={{ fill: chart.axisLabel, fontSize: 12 }}
          />
          <PolarRadiusAxis
            angle={90}
            domain={[0, 100]}
            tick={{ fill: chart.axisTick, fontSize: 10 }}
            tickCount={5}
          />
          <Tooltip
            contentStyle={chart.tooltip}
            formatter={(value: number, name: string) => [`${value}分`, name]}
          />
          {rankings.map((r, i) => {
            const color = RADAR_COLORS[i % RADAR_COLORS.length];
            return (
              <Radar
                key={r.school.id}
                name={r.school.name}
                dataKey={r.school.name}
                stroke={color.stroke}
                fill={color.fill}
                strokeWidth={2}
              />
            );
          })}
          <Legend
            wrapperStyle={{ fontSize: "12px", paddingTop: "8px" }}
            iconType="circle"
            iconSize={8}
          />
        </RechartsRadarChart>
      </ResponsiveContainer>
    </div>
  );
}
