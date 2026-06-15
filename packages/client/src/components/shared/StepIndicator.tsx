import { motion } from "framer-motion";
import { Check } from "lucide-react";
import { cn } from "@/lib/utils";

interface StepIndicatorProps {
  steps: string[];
  currentStep: number;
  className?: string;
}

export function StepIndicator({
  steps,
  currentStep,
  className,
}: StepIndicatorProps) {
  return (
    <div
      className={cn(
        "surface-panel mx-auto flex w-full max-w-4xl flex-col gap-4 rounded-[28px] px-4 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6",
        className,
      )}
    >
      {steps.map((step, index) => {
        const isCompleted = index < currentStep;
        const isCurrent = index === currentStep;
        const isAnalysisStep = /分析/.test(step);

        return (
          <div key={step} className="flex flex-1 items-center gap-3">
            <div
              className={cn(
                "relative flex h-10 w-10 shrink-0 items-center justify-center rounded-full text-sm font-semibold transition-all",
                isCompleted && "bg-warm-500 text-white",
                isCurrent && "warm-ring bg-warm-100 text-warm-700",
                !isCompleted && !isCurrent && "bg-muted text-neutral-400",
              )}
            >
              {isCurrent && isAnalysisStep && (
                <>
                  <motion.span
                    aria-hidden
                    className="absolute -inset-1 rounded-full border-2 border-warm-300/80 shadow-[0_0_0_6px_rgba(251,191,36,0.08)]"
                    animate={{ scale: [1, 1.9], opacity: [0.7, 0] }}
                    transition={{
                      duration: 1.9,
                      repeat: Infinity,
                      ease: "easeOut",
                    }}
                  />
                  <motion.span
                    aria-hidden
                    className="absolute -inset-2 rounded-full border-2 border-peach-300/70"
                    animate={{ scale: [0.95, 2.2], opacity: [0.6, 0] }}
                    transition={{
                      duration: 1.9,
                      repeat: Infinity,
                      ease: "easeOut",
                      delay: 0.38,
                    }}
                  />
                  <motion.span
                    aria-hidden
                    className="absolute -inset-3 rounded-full border border-warm-200/55"
                    animate={{ scale: [0.9, 2.45], opacity: [0.45, 0] }}
                    transition={{
                      duration: 1.9,
                      repeat: Infinity,
                      ease: "easeOut",
                      delay: 0.76,
                    }}
                  />
                </>
              )}
              {isCompleted ? <Check className="h-4 w-4" /> : index + 1}
            </div>

            <div className="min-w-0 flex-1">
              <p
                className={cn(
                  "text-sm font-medium",
                  isCurrent ? "text-warm-700" : "text-neutral-500",
                  isCompleted && "text-neutral-700",
                )}
              >
                {step}
              </p>
              <div className="mt-2 h-1.5 rounded-full bg-muted">
                <div
                  className={cn(
                    "h-full rounded-full transition-all duration-300",
                    isCompleted || isCurrent
                      ? "bg-linear-to-r from-warm-400 to-peach-400"
                      : "bg-transparent",
                    isCompleted ? "w-full" : isCurrent ? "w-2/3" : "w-0",
                  )}
                />
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}
