import { motion, AnimatePresence } from "framer-motion";
import { Clock, MapPin, Trash2, X } from "lucide-react";
import { SchoolTypeLabels } from "@/types/school";
import type { SchoolType } from "@/types/school";
import { Button } from "@/components/ui/button";
import type { SearchHistoryItem } from "@/hooks/useSearchHistory";

interface SearchHistoryCardProps {
  history: SearchHistoryItem[];
  onReplay: (item: SearchHistoryItem) => void;
  onRemove: (id: string) => void;
  onClear: () => void;
}

const TYPE_COLORS: Record<SchoolType, string> = {
  kindergarten: "bg-peach-100 text-peach-600",
  primary: "bg-sky-100 text-sky-600",
  junior_high: "bg-sage-100 text-sage-600",
  senior_high: "bg-gold-100 text-gold-600",
};

function formatTime(ts: number) {
  const d = new Date(ts);
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffMin = Math.floor(diffMs / 60_000);
  if (diffMin < 1) return "刚刚";
  if (diffMin < 60) return `${diffMin} 分钟前`;
  const diffH = Math.floor(diffMin / 60);
  if (diffH < 24) return `${diffH} 小时前`;
  const diffD = Math.floor(diffH / 24);
  if (diffD < 7) return `${diffD} 天前`;
  return d.toLocaleDateString("zh-CN", { month: "short", day: "numeric" });
}

export function SearchHistoryCard({
  history,
  onReplay,
  onRemove,
  onClear,
}: SearchHistoryCardProps) {
  if (history.length === 0) return null;

  return (
    <section className="mx-auto max-w-3xl px-4 sm:px-6">
      <div className="surface-elevated rounded-[32px] p-5 sm:p-6">
        <div className="mb-4 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Clock className="h-4 w-4 text-warm-500" />
            <h3 className="text-sm font-semibold text-neutral-800">最近搜索</h3>
          </div>
          <Button
            variant="ghost"
            size="sm"
            onClick={onClear}
            className="h-7 gap-1 text-xs text-neutral-400 hover:text-neutral-600"
          >
            <Trash2 className="h-3 w-3" />
            清空
          </Button>
        </div>

        <AnimatePresence mode="popLayout">
          {history.slice(0, 5).map((item) => (
            <motion.div
              key={item.id}
              layout
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.95 }}
              className="group flex items-center gap-3 rounded-2xl bg-background/60 px-4 py-3 transition-colors hover:bg-warm-50"
            >
              <div
                className={
                  "flex h-8 w-8 shrink-0 items-center justify-center rounded-xl text-xs font-bold " +
                  TYPE_COLORS[item.schoolType]
                }
              >
                {SchoolTypeLabels[item.schoolType][0]}
              </div>

              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <span className="truncate text-sm font-medium text-neutral-800">
                    {SchoolTypeLabels[item.schoolType]}
                  </span>
                  <span className="rounded-full bg-neutral-100 px-2 py-0.5 text-xs text-neutral-500">
                    {(item.radius / 1000).toFixed(0)} km
                  </span>
                </div>
                <p className="mt-0.5 flex items-center gap-1 truncate text-xs text-neutral-400">
                  <MapPin className="h-3 w-3 shrink-0" />
                  {item.address}
                </p>
              </div>

              <div className="flex items-center gap-1.5">
                <span className="text-xs text-neutral-400">
                  {formatTime(item.timestamp)}
                </span>
                <button
                  onClick={() => onRemove(item.id)}
                  className="rounded-full p-1 text-neutral-300 opacity-0 transition-all hover:bg-peach-50 hover:text-peach-500 group-hover:opacity-100"
                  aria-label="删除记录"
                >
                  <X className="h-3.5 w-3.5" />
                </button>
                <Button
                  size="sm"
                  variant="ghost"
                  onClick={() => onReplay(item)}
                  className="h-7 rounded-full px-3 text-xs text-warm-600 opacity-0 transition-all hover:bg-warm-100 group-hover:opacity-100"
                >
                  重搜
                </Button>
              </div>
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </section>
  );
}
