import { useState, useCallback, useMemo } from "react";
import { motion } from "framer-motion";
import { GraduationCap, MapPin, Target } from "lucide-react";
import { Button } from "@/components/ui/button";
import { getProvinces, getCitiesByProvince } from "@/data/provinceCities";

const PROVINCES = getProvinces();

interface HighSchoolRecommendDialogProps {
  onConfirm: (studentScore: number, province: string, city?: string) => void;
  onCancel: () => void;
}

export function HighSchoolRecommendDialog({
  onConfirm,
  onCancel,
}: HighSchoolRecommendDialogProps) {
  const [studentScore, setStudentScore] = useState<string>("");
  const [province, setProvince] = useState<string>("");
  const [city, setCity] = useState<string>("");
  const [scoreError, setScoreError] = useState<string>("");

  // 根据选中的省份获取地级市列表
  const cities = useMemo(() => {
    return province ? getCitiesByProvince(province) : [];
  }, [province]);

  const handleScoreChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const raw = e.target.value.replace(/[^\d]/g, "");
      if (raw.length > 4) return;
      setStudentScore(raw);
      setScoreError("");
    },
    [],
  );

  const score = parseInt(studentScore, 10);
  const canSubmit =
    !isNaN(score) && score >= 100 && score <= 900 && province.trim().length > 0;

  const handleConfirm = useCallback(() => {
    if (!canSubmit) {
      if (isNaN(score) || score < 100 || score > 900) {
        setScoreError("请输入 100–900 之间的有效分数");
      }
      return;
    }
    onConfirm(score, province, city || undefined);
  }, [canSubmit, score, province, city, onConfirm]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === "Enter" && canSubmit) {
        handleConfirm();
      }
    },
    [canSubmit, handleConfirm],
  );

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      className="mx-auto max-w-2xl"
    >
      <div className="rounded-2xl border border-warm-200/50 bg-gradient-to-br from-card/95 to-warm-50/30 p-6 shadow-lg backdrop-blur-sm sm:p-8">
        {/* Header */}
        <div className="mb-6 flex items-center gap-3">
          <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br from-green-100 to-emerald-100 shadow-sm">
            <GraduationCap className="h-6 w-6 text-green-600" />
          </div>
          <div>
            <h2 className="font-display text-xl font-semibold text-neutral-900">
              高中择校推荐
            </h2>
            <p className="text-sm text-neutral-500">
              输入摸底考试分数和所在省份，AI 为你推荐"冲、稳、保"三档高中
            </p>
          </div>
        </div>

        {/* Score Input */}
        <div className="mb-5">
          <label className="mb-2 flex items-center gap-2 text-sm font-medium text-neutral-700">
            <Target className="h-4 w-4 text-warm-500" />
            摸底考试分数
          </label>
          <div className="flex items-center gap-3">
            <input
              type="number"
              min={100}
              max={900}
              placeholder="输入分数，如 620"
              value={studentScore}
              onChange={handleScoreChange}
              onKeyDown={handleKeyDown}
              className="w-full rounded-xl border border-border/70 bg-card px-4 py-3 text-lg font-medium text-neutral-900 shadow-sm outline-none transition-all focus:border-warm-400 focus:ring-2 focus:ring-warm-200"
            />
            <span className="shrink-0 text-sm text-neutral-400">分</span>
          </div>
          {scoreError && (
            <p className="mt-1.5 text-sm text-red-500">{scoreError}</p>
          )}
          <p className="mt-1.5 text-xs text-neutral-400">
            中考总分一般 600–820 分，请以当地实际满分为准
          </p>
        </div>

        {/* Province Select */}
        <div className="mb-5">
          <label className="mb-2 flex items-center gap-2 text-sm font-medium text-neutral-700">
            <MapPin className="h-4 w-4 text-green-500" />
            所在省份
          </label>
          <select
            value={province}
            onChange={(e) => {
              setProvince(e.target.value);
              setCity(""); // 重置城市选择
            }}
            className="w-full rounded-xl border border-border/70 bg-card px-4 py-3 text-base text-neutral-900 shadow-sm outline-none transition-all focus:border-green-400 focus:ring-2 focus:ring-green-200"
          >
            <option value="">请选择省份</option>
            {PROVINCES.map((p) => (
              <option key={p} value={p}>
                {p}
              </option>
            ))}
          </select>
        </div>

        {/* City Select - 仅在选择省份后显示 */}
        {province && cities.length > 0 && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: "auto" }}
            className="mb-6"
          >
            <label className="mb-2 flex items-center gap-2 text-sm font-medium text-neutral-700">
              <MapPin className="h-4 w-4 text-emerald-500" />
              所在城市
              <span className="text-xs font-normal text-neutral-400">
                (可选)
              </span>
            </label>
            <select
              value={city}
              onChange={(e) => setCity(e.target.value)}
              className="w-full rounded-xl border border-border/70 bg-card px-4 py-3 text-base text-neutral-900 shadow-sm outline-none transition-all focus:border-emerald-400 focus:ring-2 focus:ring-emerald-200"
            >
              <option value="">全部城市</option>
              {cities.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
            <p className="mt-1.5 text-xs text-neutral-400">
              选择具体城市可获得更精准的推荐结果
            </p>
          </motion.div>
        )}

        {/* Preview */}
        {canSubmit && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: "auto" }}
            className="mb-6 rounded-xl bg-gradient-to-r from-green-50 to-emerald-50/50 px-4 py-3 border border-green-100/60"
          >
            <p className="text-sm text-green-800">
              将根据{" "}
              <span className="font-semibold">
                {province}
                {city ? ` ${city}` : ""}
              </span>{" "}
              高中的录取数据， 为{" "}
              <span className="font-semibold">{score} 分</span>{" "}
              的成绩推荐冲/稳/保三档学校
            </p>
          </motion.div>
        )}

        {/* Actions */}
        <div className="flex gap-3">
          <Button variant="outline" onClick={onCancel} className="flex-1">
            返回
          </Button>
          <Button
            variant="warm"
            onClick={handleConfirm}
            disabled={!canSubmit}
            className="flex-1"
          >
            开始分析推荐
          </Button>
        </div>
      </div>
    </motion.div>
  );
}
