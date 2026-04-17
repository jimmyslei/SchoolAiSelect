import { useState } from "react";
import { AnimatePresence, motion } from "framer-motion";
import {
  AlertCircle,
  ChevronDown,
  Loader2,
  MapPin,
  Navigation,
  Sparkles,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { GeoLocationStatus } from "@/types/school";

interface LocationStepProps {
  status: GeoLocationStatus;
  error: string | null;
  onRetry: () => void;
  onManualInput: (address: string) => void;
}

export function LocationStep({
  status,
  error,
  onRetry,
  onManualInput,
}: LocationStepProps) {
  const [manualAddress, setManualAddress] = useState("");
  const [showManual, setShowManual] = useState(false);

  const isRequesting = status === "requesting";
  const isFailed = status === "denied" || status === "error";
  const isSuccess = status === "success";

  return (
    <motion.section
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      className="surface-elevated mx-auto max-w-3xl rounded-[32px] p-6 sm:p-8"
    >
      <div className="grid gap-8 lg:grid-cols-[0.9fr_1.1fr] lg:items-center">
        <div className="space-y-4">
          <div className="inline-flex items-center gap-2 rounded-full bg-warm-100 px-4 py-2 text-xs font-medium text-warm-700">
            <Sparkles className="h-3.5 w-3.5" />
            定位与地址确认
          </div>
          <div>
            <h2 className="font-display text-2xl font-semibold text-neutral-900 sm:text-3xl">
              先确认您的位置，
              <span className="block">再开始附近学校分析。</span>
            </h2>
            <p className="mt-3 text-sm leading-7 text-neutral-600">
              系统会通过后端接口基于当前访问 IP
              自动定位；如果定位失败，也可以直接输入地址继续，不影响后续搜索逻辑。
            </p>
          </div>
        </div>

        <div className="surface-panel rounded-[28px] p-5 sm:p-6">
          {isRequesting && (
            <div className="flex flex-col items-center text-center">
              <div className="relative mb-6 flex h-24 w-24 items-center justify-center rounded-full bg-warm-100">
                <motion.div
                  animate={{ scale: [1, 1.45, 1], opacity: [0.35, 0, 0.35] }}
                  transition={{ duration: 2.2, repeat: Infinity }}
                  className="absolute inset-0 rounded-full bg-warm-300/40"
                />
                <motion.div
                  animate={{ scale: [1, 1.25, 1], opacity: [0.45, 0, 0.45] }}
                  transition={{ duration: 2.2, repeat: Infinity, delay: 0.25 }}
                  className="absolute inset-2 rounded-full bg-peach-200/40"
                />
                <Navigation className="relative h-9 w-9 text-warm-600" />
              </div>
              <h3 className="text-xl font-semibold text-neutral-900">
                正在获取您的位置
              </h3>
              <p className="mt-2 inline-flex items-center gap-2 text-sm text-neutral-500">
                <Loader2 className="h-4 w-4 animate-spin" />
                正在通过服务端接口获取当前位置
              </p>
            </div>
          )}

          {isFailed && (
            <div className="flex flex-col items-center text-center">
              <div className="mb-5 flex h-20 w-20 items-center justify-center rounded-full bg-peach-100 text-peach-500">
                <AlertCircle className="h-9 w-9" />
              </div>
              <h3 className="text-xl font-semibold text-neutral-900">
                定位未成功
              </h3>
              <p className="mt-2 max-w-sm text-sm leading-7 text-neutral-500">
                {error}
              </p>
              <div className="mt-6 w-full max-w-md space-y-3">
                <Button onClick={onRetry} variant="outline" className="w-full">
                  重新尝试定位
                </Button>
                <ManualAddressInput
                  value={manualAddress}
                  onChange={setManualAddress}
                  onSubmit={onManualInput}
                />
              </div>
            </div>
          )}

          {isSuccess && (
            <div className="flex flex-col items-center text-center">
              <motion.div
                initial={{ scale: 0.8, opacity: 0 }}
                animate={{ scale: 1, opacity: 1 }}
                transition={{ duration: 0.35 }}
                className="mb-5 flex h-20 w-20 items-center justify-center rounded-full bg-sage-100 text-sage-500"
              >
                <MapPin className="h-9 w-9" />
              </motion.div>
              <h3 className="text-xl font-semibold text-neutral-900">
                位置获取成功
              </h3>
              <p className="mt-2 text-sm leading-7 text-neutral-500">
                已准备进入学校搜索与分析阶段，请稍候。
              </p>
            </div>
          )}

          {isRequesting && (
            <div className="mt-6 border-t border-border/70 pt-5">
              <button
                type="button"
                onClick={() => setShowManual((value) => !value)}
                className="flex w-full items-center justify-center gap-2 text-sm text-neutral-500 transition-colors hover:text-warm-700"
              >
                自动定位不准确？改为手动输入地址
                <ChevronDown
                  className={cn(
                    "h-4 w-4 transition-transform duration-200",
                    showManual && "rotate-180",
                  )}
                />
              </button>

              <AnimatePresence>
                {showManual && (
                  <motion.div
                    initial={{ opacity: 0, height: 0 }}
                    animate={{ opacity: 1, height: "auto" }}
                    exit={{ opacity: 0, height: 0 }}
                    className="overflow-hidden"
                  >
                    <div className="pt-4">
                      <ManualAddressInput
                        value={manualAddress}
                        onChange={setManualAddress}
                        onSubmit={onManualInput}
                      />
                    </div>
                  </motion.div>
                )}
              </AnimatePresence>
            </div>
          )}
        </div>
      </div>
    </motion.section>
  );
}

function ManualAddressInput({
  value,
  onChange,
  onSubmit,
}: {
  value: string;
  onChange: (value: string) => void;
  onSubmit: (value: string) => void;
}) {
  return (
    <div className="space-y-3">
      <div className="relative">
        <MapPin className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-neutral-400" />
        <Input
          type="text"
          value={value}
          onChange={(event) => onChange(event.target.value)}
          placeholder="请输入地址，例如：北京市海淀区中关村..."
          className="pl-11"
          onKeyDown={(event) => {
            if (event.key === "Enter" && value.trim()) {
              onSubmit(value.trim());
            }
          }}
        />
      </div>
      <Button
        onClick={() => value.trim() && onSubmit(value.trim())}
        disabled={!value.trim()}
        className="w-full"
      >
        使用该地址继续
      </Button>
    </div>
  );
}
