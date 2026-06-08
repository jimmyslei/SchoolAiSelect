import { motion } from "framer-motion";
import { MapPin } from "lucide-react";

export interface RadiusSelectorProps {
  value: number;
  onChange: (radius: number) => void;
}

const RADIUS_OPTIONS = [
  { value: 1000, label: "1 km", description: "步行范围" },
  { value: 3000, label: "3 km", description: "骑行范围" },
  { value: 5000, label: "5 km", description: "推荐范围" },
  { value: 10000, label: "10 km", description: "较大范围" },
];

export function RadiusSelector({ value, onChange }: RadiusSelectorProps) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      <span className="mr-1 flex items-center gap-1.5 text-sm text-neutral-600">
        <MapPin className="h-3.5 w-3.5 text-warm-500" />
        搜索半径
      </span>
      {RADIUS_OPTIONS.map((opt) => {
        const isSelected = value === opt.value;
        return (
          <button
            key={opt.value}
            onClick={() => onChange(opt.value)}
            className={
              "relative rounded-full px-4 py-1.5 text-sm font-medium transition-colors " +
              (isSelected
                ? "bg-warm-600 text-white shadow-sm"
                : "bg-warm-50 text-neutral-600 hover:bg-warm-100")
            }
          >
            {isSelected && (
              <motion.span
                layoutId="radius-pill"
                className="absolute inset-0 rounded-full bg-warm-600"
                transition={{ type: "spring", stiffness: 400, damping: 30 }}
              />
            )}
            <span className="relative z-10">{opt.label}</span>
            <span
              className={
                "relative z-10 ml-1.5 text-xs " +
                (isSelected ? "text-warm-100" : "text-neutral-400")
              }
            >
              {opt.description}
            </span>
          </button>
        );
      })}
    </div>
  );
}
