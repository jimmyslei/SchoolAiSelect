import {
  BarChart as RechartsBarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Cell,
  LabelList,
} from "recharts";
import type { SchoolRanking } from "@/types/school";
import { useIsDark } from "@/hooks/useTheme";
import { getChartTheme } from "@/lib/chartTheme";

interface ComparisonBarChartProps {
  rankings: SchoolRanking[];
}

const COLORS = [
  "#ffc107",
  "#a8a49c",
  "#ed8c42",
  "#6daf6d",
  "#60adff",
  "#ff8a5c",
  "#9fcc9f",
  "#93cbff",
];

export function ComparisonBarChart({ rankings }: ComparisonBarChartProps) {
  const isDark = useIsDark();
  const chart = getChartTheme(isDark);

  const data = rankings.slice(0, 8).map((r) => ({
    name:
      r.school.name.length > 6
        ? r.school.name.slice(0, 6) + "…"
        : r.school.name,
    score: r.overallScore,
    fullName: r.school.name,
  }));

  return (
    <div className="w-full">
      <h4 className="text-sm font-medium text-neutral-600 mb-3 text-center">
        学校综合评分对比
      </h4>
      <ResponsiveContainer
        width="100%"
        height={Math.max(200, data.length * 44)}
      >
        <RechartsBarChart
          data={data}
          layout="vertical"
          margin={{ left: 0, right: 40, top: 5, bottom: 5 }}
        >
          <CartesianGrid
            strokeDasharray="3 3"
            stroke={chart.gridSoft}
            horizontal={false}
          />
          <XAxis
            type="number"
            domain={[0, 100]}
            tick={{ fill: chart.axisTick, fontSize: 11 }}
          />
          <YAxis
            type="category"
            dataKey="name"
            width={70}
            tick={{ fill: chart.valueLabel, fontSize: 11 }}
          />
          <Tooltip
            contentStyle={chart.tooltip}
            formatter={(value: number, _name: string, props: any) => [
              `${value}分`,
              props?.payload?.fullName ?? "",
            ]}
          />
          <Bar dataKey="score" radius={[0, 6, 6, 0]} barSize={24}>
            {data.map((_entry, index) => (
              <Cell
                key={index}
                fill={COLORS[index % COLORS.length]}
                fillOpacity={0.85}
              />
            ))}
            <LabelList
              dataKey="score"
              position="right"
              style={{ fill: chart.valueLabel, fontSize: 12, fontWeight: 600 }}
            />
          </Bar>
        </RechartsBarChart>
      </ResponsiveContainer>
    </div>
  );
}
