import { useState, useCallback } from "react";
import { GraduationCap, AlertCircle } from "lucide-react";
import { cn } from "@/lib/utils";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";

interface ScoreInputDialogProps {
  open: boolean;
  onClose: () => void;
  onConfirm: (score: number) => void;
}

export function ScoreInputDialog({
  open,
  onClose,
  onConfirm,
}: ScoreInputDialogProps) {
  const [score, setScore] = useState("");
  const [error, setError] = useState("");

  const handleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const raw = e.target.value.replace(/[^\d]/g, "");
    if (raw.length > 4) return;
    setScore(raw);
    setError("");
  }, []);

  const handleSubmit = useCallback(() => {
    const num = parseInt(score, 10);
    if (isNaN(num) || num < 100 || num > 800) {
      setError("请输入 100–800 之间的有效分数");
      return;
    }
    onConfirm(num);
    setScore("");
    setError("");
  }, [score, onConfirm]);

  const handleClose = useCallback(() => {
    setScore("");
    setError("");
    onClose();
  }, [onClose]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === "Enter") {
        handleSubmit();
      }
    },
    [handleSubmit],
  );

  return (
    <Dialog open={open} onOpenChange={(value) => !value && handleClose()}>
      <DialogContent className="gap-0 overflow-hidden p-0 sm:max-w-md">
        <div className="border-b border-border/70 px-6 pb-5 pt-8 sm:px-8">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <GraduationCap className="h-5 w-5 text-warm-500" />
              输入考试分数
            </DialogTitle>
            <DialogDescription>
              请输入学生的中考/高考分数，系统将根据历年录取分数线为您分析录取可能性。
            </DialogDescription>
          </DialogHeader>
        </div>

        <div className="space-y-4 px-6 py-6 sm:px-8">
          <div>
            <label
              htmlFor="student-score"
              className="text-sm font-medium text-neutral-700"
            >
              考试总分
            </label>
            <div className="relative mt-2">
              <input
                id="student-score"
                type="text"
                inputMode="numeric"
                placeholder="例如：620"
                value={score}
                onChange={handleChange}
                onKeyDown={handleKeyDown}
                autoFocus
                className={cn(
                  "h-12 w-full rounded-xl border bg-background px-4 text-center text-lg font-semibold tracking-wide outline-none transition-colors",
                  "placeholder:text-neutral-400 focus:border-warm-400 focus:ring-2 focus:ring-warm-100",
                  error
                    ? "border-red-300 focus:border-red-400 focus:ring-red-100"
                    : "border-border/70",
                )}
              />
              <span className="absolute right-4 top-1/2 -translate-y-1/2 text-sm text-neutral-400">
                分
              </span>
            </div>
            {error && (
              <p className="mt-2 flex items-center gap-1.5 text-sm text-red-500">
                <AlertCircle className="h-3.5 w-3.5 shrink-0" />
                {error}
              </p>
            )}
          </div>

          <div className="rounded-xl bg-neutral-50 px-4 py-3 text-sm leading-6 text-neutral-500">
            <p className="font-medium text-neutral-700">参考分值范围</p>
            <p className="mt-1">
              中考总分一般 600–800 分，高考总分一般 750
              分。请以当地实际满分为准。
            </p>
          </div>
        </div>

        <div className="flex gap-3 border-t border-border/70 px-6 py-4 sm:px-8">
          <Button
            variant="outline"
            className="h-11 flex-1 rounded-xl"
            onClick={handleClose}
          >
            跳过
          </Button>
          <Button
            className="h-11 flex-1 rounded-xl bg-warm-500 text-white hover:bg-warm-600"
            onClick={handleSubmit}
            disabled={!score}
          >
            确认并搜索
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
