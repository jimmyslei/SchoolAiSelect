import { cn } from "@/lib/utils";

interface LoadingSpinnerProps {
  className?: string;
  size?: "sm" | "md" | "lg";
  label?: string;
}

export function LoadingSpinner({
  className,
  size = "md",
  label,
}: LoadingSpinnerProps) {
  const sizeClasses = {
    sm: "w-5 h-5 border-2",
    md: "w-8 h-8 border-[3px]",
    lg: "w-12 h-12 border-4",
  };

  return (
    <div className={cn("flex flex-col items-center gap-3", className)}>
      <div
        className={cn(
          "rounded-full border-warm-200 border-t-warm-500 animate-spin",
          sizeClasses[size],
        )}
      />
      {label && (
        <p className="text-sm text-neutral-500 animate-pulse">{label}</p>
      )}
    </div>
  );
}
