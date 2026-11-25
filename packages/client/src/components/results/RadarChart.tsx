import {
  Radar,
  RadarChart as RechartsRadarChart,
  PolarGrid,
  PolarAngleAxis,
  PolarRadiusAxis,
  ResponsiveContainer,
  Tooltip,
} from "recharts";
import type { SchoolAnalysis } from "@/types/school";
import { useIsDark } from "@/hooks/useTheme";
import { getChartTheme } from "@/lib/chartTheme";

interface SchoolRadarChartProps {
  analysis: SchoolAnalysis;
  schoolName: string;
}

export function SchoolRadarChart({
  analysis,
  schoolName,
}: SchoolRadarChartProps) {
  const isDark = useIsDark();
  const chart = getChartTheme(isDark);

  const data = [
    {
      dimension: "师资力量",
      score: analysis.teachingStaffScore,
      fullMark: 100,
    },
    { dimension: "用户评价", score: analysis.reputationScore, fullMark: 100 },
    {
      dimension: "文章质量",
      score: analysis.contentQualityScore,
      fullMark: 100,
    },
    { dimension: "网络活跃", score: analysis.activityScore, fullMark: 100 },
    {
      dimension: "学生活动",
      score: analysis.studentActivityScore,
      fullMark: 100,
    },
    {
      dimension: "获奖荣誉",
      score: Math.min(analysis.awardsCount * 5, 100),
      fullMark: 100,
    },
  ];

  return (
    <div className="w-full">
      <h4 className="text-sm font-medium text-neutral-600 mb-2 text-center">
        多维度评分
      </h4>
      <ResponsiveContainer width="100%" height={280}>
        <RechartsRadarChart data={data} cx="50%" cy="50%" outerRadius="72%">
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
            formatter={(value: number) => [`${value}分`, ""]}
          />
          <Radar
            name={schoolName}
            dataKey="score"
            stroke="#ed8c42"
            fill="#ed8c42"
            fillOpacity={0.2}
            strokeWidth={2}
          />
        </RechartsRadarChart>
      </ResponsiveContainer>
    </div>
  );
}
