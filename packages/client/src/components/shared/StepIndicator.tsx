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

        return (
          <div key={step} className="flex flex-1 items-center gap-3">
            <div
              className={cn(
                "flex h-10 w-10 shrink-0 items-center justify-center rounded-full text-sm font-semibold transition-all",
                isCompleted && "bg-warm-500 text-white",
                isCurrent && "warm-ring bg-warm-100 text-warm-700",
                !isCompleted && !isCurrent && "bg-muted text-neutral-400",
              )}
            >
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
