import * as React from "react";
import { cn } from "@/lib/utils";

function Input({ className, type, ...props }: React.ComponentProps<"input">) {
  return (
    <input
      type={type}
      className={cn(
        "flex h-12 w-full rounded-2xl border border-input bg-background/90 px-4 py-3 text-sm text-foreground shadow-xs ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 transition-[border-color,box-shadow,background-color]",
        className,
      )}
      {...props}
    />
  );
}

export { Input };
