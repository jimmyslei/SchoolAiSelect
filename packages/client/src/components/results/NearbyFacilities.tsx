import { useState, useEffect } from "react";
import { motion, AnimatePresence } from "framer-motion";
import {
  MapPin,
  Train,
  Building2,
  ShoppingCart,
  Trees,
  BookOpen,
  BookMarked,
  ChevronDown,
  Loader2,
} from "lucide-react";
import { fetchNearbyFacilities, type NearbyFacility } from "@/lib/api";
import { cn } from "@/lib/utils";

interface FacilityCategory {
  key: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  color: string;
  bgColor: string;
}

const CATEGORIES: FacilityCategory[] = [
  {
    key: "subway",
    label: "地铁站",
    icon: Train,
    color: "text-blue-600",
    bgColor: "bg-blue-50",
  },
  {
    key: "hospital",
    label: "医院",
    icon: Building2,
    color: "text-red-500",
    bgColor: "bg-red-50",
  },
  {
    key: "supermarket",
    label: "超市",
    icon: ShoppingCart,
    color: "text-orange-500",
    bgColor: "bg-orange-50",
  },
  {
    key: "park",
    label: "公园",
    icon: Trees,
    color: "text-green-600",
    bgColor: "bg-green-50",
  },
  {
    key: "library",
    label: "图书馆",
    icon: BookOpen,
    color: "text-violet-600",
    bgColor: "bg-violet-50",
  },
  {
    key: "bookstore",
    label: "书店",
    icon: BookMarked,
    color: "text-amber-600",
    bgColor: "bg-amber-50",
  },
];

interface NearbyFacilitiesProps {
  latitude: number;
  longitude: number;
  schoolName: string;
}

function formatDistance(meters: string): string {
  const m = parseInt(meters, 10);
  if (isNaN(m)) return "未知";
  if (m < 1000) return `${m}m`;
  return `${(m / 1000).toFixed(1)}km`;
}

function getDistanceColor(meters: string): string {
  const m = parseInt(meters, 10);
  if (isNaN(m)) return "text-neutral-400";
  if (m <= 500) return "text-green-600 font-semibold";
  if (m <= 1000) return "text-green-500";
  if (m <= 2000) return "text-yellow-600";
  return "text-neutral-500";
}

export function NearbyFacilities({
  latitude,
  longitude,
  schoolName,
}: NearbyFacilitiesProps) {
  const [activeCategory, setActiveCategory] = useState<string>("subway");
  const [facilities, setFacilities] = useState<
    Record<string, NearbyFacility[]>
  >({});
  const [loading, setLoading] = useState<Record<string, boolean>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [expanded, setExpanded] = useState(true);

  useEffect(() => {
    let cancelled = false;
    const cat = activeCategory;

    if (facilities[cat]) return; // already loaded

    setLoading((prev) => ({ ...prev, [cat]: true }));
    setErrors((prev) => ({ ...prev, [cat]: "" }));

    fetchNearbyFacilities(latitude, longitude, cat, 2000)
      .then((data) => {
        if (cancelled) return;
        setFacilities((prev) => ({ ...prev, [cat]: data }));
      })
      .catch((err) => {
        if (cancelled) return;
        setErrors((prev) => ({ ...prev, [cat]: err.message }));
      })
      .finally(() => {
        if (cancelled) return;
        setLoading((prev) => ({ ...prev, [cat]: false }));
      });

    return () => {
      cancelled = true;
    };
  }, [activeCategory, latitude, longitude]);

  const activeCat = CATEGORIES.find((c) => c.key === activeCategory)!;
  const currentFacilities = facilities[activeCategory] || [];
  const isLoading = loading[activeCategory];
  const error = errors[activeCategory];

  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: 0.1 }}
      className="mt-4"
    >
      {/* 标题栏 */}
      <button
        onClick={() => setExpanded(!expanded)}
        className="w-full flex items-center justify-between py-2 text-left group"
      >
        <div className="flex items-center gap-2">
          <MapPin className="w-4 h-4 text-warm-500" />
          <span className="text-sm font-semibold text-neutral-700">
            周边生活设施
          </span>
          <span className="text-xs text-neutral-400">2km范围内</span>
        </div>
        <ChevronDown
          className={cn(
            "w-4 h-4 text-neutral-400 transition-transform duration-200",
            expanded && "rotate-180",
          )}
        />
      </button>

      <AnimatePresence>
        {expanded && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: "auto", opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="overflow-hidden"
          >
            {/* 分类标签 */}
            <div className="flex gap-1.5 overflow-x-auto pb-3 scrollbar-none -mx-0.5">
              {CATEGORIES.map((cat) => {
                const Icon = cat.icon;
                const isActive = cat.key === activeCategory;
                const count = facilities[cat.key]?.length;
                return (
                  <button
                    key={cat.key}
                    onClick={() => setActiveCategory(cat.key)}
                    className={cn(
                      "flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium whitespace-nowrap transition-all shrink-0",
                      isActive
                        ? `${cat.bgColor} ${cat.color} ring-1 ring-current/20`
                        : "bg-neutral-50 text-neutral-500 hover:bg-neutral-100",
                    )}
                  >
                    <Icon className="w-3.5 h-3.5" />
                    {cat.label}
                    {count !== undefined && (
                      <span
                        className={cn(
                          "text-[10px] px-1 rounded-full min-w-4 text-center",
                          isActive ? "bg-white/60 dark:bg-black/25" : "bg-neutral-200/60",
                        )}
                      >
                        {count}
                      </span>
                    )}
                  </button>
                );
              })}
            </div>

            {/* 设施列表 */}
            <div className="space-y-1.5">
              {isLoading ? (
                <div className="flex items-center justify-center py-8 gap-2 text-neutral-400">
                  <Loader2 className="w-4 h-4 animate-spin" />
                  <span className="text-xs">查询{activeCat.label}...</span>
                </div>
              ) : error ? (
                <div className="text-center py-6 text-xs text-neutral-400">
                  {error}
                </div>
              ) : currentFacilities.length === 0 ? (
                <div className="text-center py-6 text-xs text-neutral-400">
                  2km内未找到{activeCat.label}
                </div>
              ) : (
                currentFacilities.slice(0, 6).map((f, i) => (
                  <motion.div
                    key={f.id}
                    initial={{ opacity: 0, x: -8 }}
                    animate={{ opacity: 1, x: 0 }}
                    transition={{ delay: i * 0.03 }}
                    className="flex items-center gap-3 px-3 py-2 rounded-lg bg-neutral-50/60 hover:bg-neutral-50 transition-colors group"
                  >
                    <div
                      className={cn(
                        "w-7 h-7 rounded-lg flex items-center justify-center shrink-0",
                        activeCat.bgColor,
                      )}
                    >
                      <activeCat.icon
                        className={cn("w-3.5 h-3.5", activeCat.color)}
                      />
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="text-sm text-neutral-700 font-medium truncate">
                        {f.name}
                      </div>
                      {f.address && (
                        <div className="text-[11px] text-neutral-400 truncate">
                          {f.address}
                        </div>
                      )}
                    </div>
                    <div
                      className={cn(
                        "text-xs tabular-nums shrink-0",
                        getDistanceColor(f.distance),
                      )}
                    >
                      {formatDistance(f.distance)}
                    </div>
                  </motion.div>
                ))
              )}
            </div>

            {/* 更多提示 */}
            {currentFacilities.length > 6 && (
              <div className="text-center mt-2">
                <span className="text-[11px] text-neutral-400">
                  还有 {currentFacilities.length - 6} 个{activeCat.label}
                </span>
              </div>
            )}
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  );
}
