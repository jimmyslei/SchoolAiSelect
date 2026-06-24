import { useEffect, useMemo, useRef, useState } from "react";
import { motion } from "framer-motion";
import {
  Bot,
  CheckCircle2,
  CircleDashed,
  ChevronDown,
  ChevronUp,
  TerminalSquare,
  XCircle,
} from "lucide-react";
import type { AgentExecutionStep } from "@/types/school";

interface AgentStreamingPanelProps {
  title: string;
  subtitle: string;
  color: "purple" | "green" | "warm";
  steps: AgentExecutionStep[];
  emptyHint?: string;
}

const colorMap = {
  purple: {
    badge: "bg-purple-50 text-purple-700 ring-purple-200",
    dot: "bg-purple-500",
    surface: "from-purple-50/80 via-white to-purple-50/40",
  },
  green: {
    badge: "bg-green-50 text-green-700 ring-green-200",
    dot: "bg-green-500",
    surface: "from-green-50/80 via-white to-green-50/40",
  },
  warm: {
    badge: "bg-warm-50 text-warm-700 ring-warm-200",
    dot: "bg-warm-500",
    surface: "from-warm-50/80 via-white to-sand-50/50",
  },
} as const;

function formatTime(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? "刚刚"
    : date.toLocaleTimeString("zh-CN", {
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
      });
}

function StepStatusIcon({ status }: { status: string }) {
  if (status === "completed") {
    return <CheckCircle2 className="h-4 w-4 text-emerald-600" />;
  }
  if (status === "failed") {
    return <XCircle className="h-4 w-4 text-rose-600" />;
  }
  return <CircleDashed className="h-4 w-4 animate-spin text-amber-600" />;
}

export function AgentStreamingPanel({
  title,
  subtitle,
  color,
  steps,
  emptyHint = "AI 正在建立分析上下文，稍后将实时显示工具调用与处理进度。",
}: AgentStreamingPanelProps) {
  const palette = colorMap[color];
  const scrollContainerRef = useRef<HTMLDivElement | null>(null);
  const [expandedMap, setExpandedMap] = useState<Record<string, boolean>>({});

  const normalizedSteps = useMemo(
    () =>
      steps.map((step, index) => ({
        ...step,
        itemKey: `${step.id}-${index}`,
      })),
    [steps],
  );

  useEffect(() => {
    setExpandedMap((current) => {
      const next: Record<string, boolean> = {};

      for (const step of normalizedSteps) {
        next[step.itemKey] =
          current[step.itemKey] ?? step.status !== "completed";
      }

      return next;
    });
  }, [normalizedSteps]);

  useEffect(() => {
    const container = scrollContainerRef.current;
    if (!container) {
      return;
    }

    container.scrollTo({
      top: container.scrollHeight,
      behavior: "smooth",
    });
  }, [normalizedSteps.length]);

  function toggleExpanded(itemKey: string) {
    setExpandedMap((current) => ({
      ...current,
      [itemKey]: !current[itemKey],
    }));
  }

  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      className={`surface-elevated overflow-hidden rounded-4xl bg-linear-to-br ${palette.surface}`}
    >
      <div className="border-b border-border/50 px-5 py-5 sm:px-6">
        <div className="flex items-start justify-between gap-4">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full bg-neutral-950 px-3 py-1 text-xs font-medium uppercase tracking-[0.2em] text-white/90">
              <Bot className="h-3.5 w-3.5" />
              Agent Live Trace
            </div>
            <h3 className="mt-3 text-lg font-semibold text-neutral-900 sm:text-xl">
              {title}
            </h3>
            <p className="mt-1 text-sm leading-6 text-neutral-600">
              {subtitle}
            </p>
          </div>
          <div
            className={`inline-flex items-center gap-2 rounded-full px-3 py-1.5 text-xs ring-1 ${palette.badge}`}
          >
            <span className={`h-2 w-2 rounded-full ${palette.dot}`} />
            {steps.length} 条步骤
          </div>
        </div>
      </div>

      <div
        ref={scrollContainerRef}
        className="max-h-112 space-y-3 overflow-y-auto px-4 py-4 sm:px-6"
      >
        {steps.length === 0 ? (
          <div className="rounded-3xl border border-dashed border-border/70 bg-white/70 px-5 py-8 text-center text-sm leading-7 text-neutral-500">
            {emptyHint}
          </div>
        ) : (
          normalizedSteps.map((step) => {
            const isExpanded =
              expandedMap[step.itemKey] ?? step.status !== "completed";
            const isCompleted = step.status === "completed";

            return (
              <motion.div
                key={step.itemKey}
                initial={{ opacity: 0, y: 8 }}
                animate={{ opacity: 1, y: 0 }}
                className="rounded-3xl border border-border/60 bg-white/80 px-4 py-4 shadow-[0_8px_24px_rgba(15,23,42,0.06)]"
              >
                <div className="flex items-start gap-3">
                  <div className="mt-0.5 flex h-8 w-8 items-center justify-center rounded-full bg-neutral-100 text-neutral-700">
                    <StepStatusIcon status={step.status} />
                  </div>
                  <div className="min-w-0 flex-1">
                    <button
                      type="button"
                      onClick={() => toggleExpanded(step.itemKey)}
                      className="flex w-full flex-wrap items-center gap-2 text-left"
                    >
                      <p className="text-sm font-semibold text-neutral-900">
                        {step.message}
                      </p>
                      <span className="text-xs text-neutral-400">
                        {formatTime(step.timestamp)}
                      </span>
                      {step.entityName && (
                        <span className="rounded-full bg-neutral-100 px-2 py-0.5 text-xs text-neutral-600">
                          {step.entityName}
                        </span>
                      )}
                      {isCompleted && (
                        <span className="ml-auto inline-flex items-center gap-1 rounded-full bg-neutral-100 px-2.5 py-1 text-[11px] text-neutral-600">
                          {isExpanded ? (
                            <ChevronUp className="h-3.5 w-3.5" />
                          ) : (
                            <ChevronDown className="h-3.5 w-3.5" />
                          )}
                          {isExpanded ? "收起" : "展开"}
                        </span>
                      )}
                    </button>

                    {(!isCompleted || isExpanded) && (
                      <>
                        {step.toolName && (
                          <div className="mt-3 rounded-2xl bg-neutral-950 px-3 py-2 text-xs text-neutral-100">
                            <div className="flex items-center gap-2 font-medium text-white">
                              <TerminalSquare className="h-3.5 w-3.5" />
                              {step.toolName}
                            </div>
                            {step.toolArguments && (
                              <pre className="mt-2 overflow-x-auto whitespace-pre-wrap break-all text-[11px] leading-5 text-neutral-300">
                                {JSON.stringify(step.toolArguments, null, 2)}
                              </pre>
                            )}
                          </div>
                        )}

                        {step.toolResult && (
                          <div className="mt-3 rounded-2xl bg-neutral-50 px-3 py-2 text-xs leading-5 text-neutral-600 ring-1 ring-inset ring-border/60">
                            <div className="mb-1 font-medium text-neutral-800">
                              结果摘要
                            </div>
                            <pre className="overflow-x-auto whitespace-pre-wrap break-all text-[11px] leading-5">
                              {step.toolResult}
                            </pre>
                          </div>
                        )}
                      </>
                    )}
                  </div>
                </div>
              </motion.div>
            );
          })
        )}
      </div>
    </motion.div>
  );
}
