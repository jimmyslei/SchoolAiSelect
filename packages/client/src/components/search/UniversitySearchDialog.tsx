import { useState, useCallback } from "react";
import { motion, AnimatePresence } from "framer-motion";
import {
  Plus,
  X,
  GraduationCap,
  BookOpen,
  Target,
  Sparkles,
  Heart,
} from "lucide-react";
import { Button } from "@/components/ui/button";

interface UniversitySearchDialogProps {
  onConfirm: (payload: {
    studentScore: number;
    universityNames?: string[];
    preferredMajors: string[];
    preferredDirections: string[];
    personalPreferences?: string;
    searchMode: "manual" | "recommend";
  }) => void;
  onCancel: () => void;
}

const POPULAR_UNIVERSITIES = [
  "清华大学",
  "北京大学",
  "浙江大学",
  "上海交通大学",
  "复旦大学",
  "南京大学",
  "中国科学技术大学",
  "华中科技大学",
  "武汉大学",
  "中山大学",
  "同济大学",
  "北京师范大学",
];

const POPULAR_MAJORS = [
  "计算机科学与技术",
  "人工智能",
  "电子信息工程",
  "软件工程",
  "临床医学",
  "金融学",
  "法学",
  "机械工程",
  "建筑学",
  "电气工程",
];

export function UniversitySearchDialog({
  onConfirm,
  onCancel,
}: UniversitySearchDialogProps) {
  const [studentScore, setStudentScore] = useState<string>("");
  const [searchMode, setSearchMode] = useState<"manual" | "recommend">(
    "manual",
  );
  const [universityNames, setUniversityNames] = useState<string[]>([]);
  const [preferredMajors, setPreferredMajors] = useState<string[]>([]);
  const [preferredDirections, setPreferredDirections] = useState<string[]>([]);
  const [uniInput, setUniInput] = useState("");
  const [majorInput, setMajorInput] = useState("");
  const [directionInput, setDirectionInput] = useState("");
  const [personalPreferences, setPersonalPreferences] = useState("");

  const addUniversity = useCallback(
    (name: string) => {
      const trimmed = name.trim();
      if (
        trimmed &&
        !universityNames.includes(trimmed) &&
        universityNames.length < 10
      ) {
        setUniversityNames((prev) => [...prev, trimmed]);
      }
    },
    [universityNames],
  );

  const removeUniversity = useCallback((name: string) => {
    setUniversityNames((prev) => prev.filter((n) => n !== name));
  }, []);

  const addMajor = useCallback(
    (name: string) => {
      const trimmed = name.trim();
      if (trimmed && !preferredMajors.includes(trimmed)) {
        setPreferredMajors((prev) => [...prev, trimmed]);
      }
    },
    [preferredMajors],
  );

  const removeMajor = useCallback((name: string) => {
    setPreferredMajors((prev) => prev.filter((n) => n !== name));
  }, []);

  const addDirection = useCallback(
    (name: string) => {
      const trimmed = name.trim();
      if (trimmed && !preferredDirections.includes(trimmed)) {
        setPreferredDirections((prev) => [...prev, trimmed]);
      }
    },
    [preferredDirections],
  );

  const removeDirection = useCallback((name: string) => {
    setPreferredDirections((prev) => prev.filter((n) => n !== name));
  }, []);

  const handleUniInputKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === "Enter") {
        e.preventDefault();
        addUniversity(uniInput);
        setUniInput("");
      }
    },
    [uniInput, addUniversity],
  );

  const handleMajorInputKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === "Enter") {
        e.preventDefault();
        addMajor(majorInput);
        setMajorInput("");
      }
    },
    [majorInput, addMajor],
  );

  const handleDirectionInputKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === "Enter") {
        e.preventDefault();
        addDirection(directionInput);
        setDirectionInput("");
      }
    },
    [directionInput, addDirection],
  );

  const score = parseInt(studentScore, 10);
  const hasPreferenceInput =
    preferredMajors.length > 0 ||
    preferredDirections.length > 0 ||
    personalPreferences.trim().length > 0;
  const canSubmit =
    !isNaN(score) &&
    score >= 100 &&
    score <= 750 &&
    (searchMode === "manual" ? universityNames.length > 0 : hasPreferenceInput);

  const handleConfirm = useCallback(() => {
    if (canSubmit) {
      onConfirm({
        studentScore: score,
        universityNames: searchMode === "manual" ? universityNames : undefined,
        preferredMajors,
        preferredDirections,
        personalPreferences: personalPreferences.trim() || undefined,
        searchMode,
      });
    }
  }, [
    canSubmit,
    score,
    searchMode,
    universityNames,
    preferredMajors,
    preferredDirections,
    personalPreferences,
    onConfirm,
  ]);

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      className="mx-auto w-full max-w-2xl"
    >
      <div className="rounded-[28px] border border-warm-200/50 bg-linear-to-br from-white/95 to-warm-50/30 p-4 shadow-lg backdrop-blur-sm sm:rounded-4xl sm:p-8">
        {/* Header */}
        <div className="mb-6 flex items-start gap-3 sm:items-center">
          <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-linear-to-br from-purple-100 to-blue-100 shadow-sm sm:h-12 sm:w-12">
            <GraduationCap className="h-5 w-5 text-purple-600 sm:h-6 sm:w-6" />
          </div>
          <div className="min-w-0">
            <h2 className="font-display text-lg font-semibold text-neutral-900 sm:text-xl">
              大学报考分析
            </h2>
            <p className="mt-1 text-sm leading-6 text-neutral-500">
              支持手动选大学，或仅输入分数与偏好让 AI 智能推荐
            </p>
          </div>
        </div>

        <div className="mb-5 grid gap-2 sm:grid-cols-2">
          <button
            type="button"
            onClick={() => setSearchMode("manual")}
            className={`rounded-2xl border px-4 py-3 text-left transition-all ${
              searchMode === "manual"
                ? "border-purple-300 bg-purple-50 shadow-sm"
                : "border-neutral-200 bg-white hover:border-purple-200"
            }`}
          >
            <div className="flex items-center gap-2 text-sm font-medium text-neutral-800">
              <GraduationCap className="h-4 w-4 text-purple-500" />
              目标选择大学
            </div>
            <p className="mt-1 text-xs leading-relaxed text-neutral-500">
              适合已有目标院校，分析录取概率、专业实力、转专业与就业情况。
            </p>
          </button>
          <button
            type="button"
            onClick={() => setSearchMode("recommend")}
            className={`rounded-2xl border px-4 py-3 text-left transition-all ${
              searchMode === "recommend"
                ? "border-blue-300 bg-blue-50 shadow-sm"
                : "border-neutral-200 bg-white hover:border-blue-200"
            }`}
          >
            <div className="flex items-center gap-2 text-sm font-medium text-neutral-800">
              <Sparkles className="h-4 w-4 text-blue-500" />
              智能推荐大学
            </div>
            <p className="mt-1 text-xs leading-relaxed text-neutral-500">
              输入分数、喜欢方向和个人偏好，自动优先推荐
              985/211、省会高校或技能型专业。
            </p>
          </button>
        </div>

        {/* Score Input */}
        <div className="mb-5">
          <label className="mb-2 flex items-center gap-2 text-sm font-medium text-neutral-700">
            <Target className="h-4 w-4 text-warm-500" />
            摸底高考成绩
          </label>
          <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-3">
            <input
              type="number"
              min={100}
              max={750}
              placeholder="输入分数，如 580"
              value={studentScore}
              onChange={(e) => setStudentScore(e.target.value)}
              className="w-full rounded-xl border border-neutral-200 bg-white px-4 py-3 text-lg font-medium text-neutral-900 shadow-sm outline-none transition-all focus:border-warm-400 focus:ring-2 focus:ring-warm-200"
            />
            <span className="shrink-0 pl-1 text-sm text-neutral-400 sm:pl-0">
              / 750
            </span>
          </div>
        </div>

        {/* University Input */}
        {searchMode === "manual" && (
          <div className="mb-5">
            <label className="mb-2 flex items-center gap-2 text-sm font-medium text-neutral-700">
              <GraduationCap className="h-4 w-4 text-purple-500" />
              意向大学
              <span className="text-xs text-neutral-400">（最多 10 所）</span>
            </label>
            <div className="mb-2 flex flex-col gap-2 sm:flex-row">
              <input
                type="text"
                placeholder="输入大学名称，按 Enter 添加"
                value={uniInput}
                onChange={(e) => setUniInput(e.target.value)}
                onKeyDown={handleUniInputKeyDown}
                className="flex-1 rounded-xl border border-neutral-200 bg-white px-4 py-2.5 text-sm shadow-sm outline-none transition-all focus:border-purple-400 focus:ring-2 focus:ring-purple-200"
              />
              <Button
                size="sm"
                variant="outline"
                onClick={() => {
                  addUniversity(uniInput);
                  setUniInput("");
                }}
                className="h-10 w-full shrink-0 sm:w-auto"
              >
                <Plus className="h-4 w-4" />
              </Button>
            </div>

            {/* Selected universities */}
            <AnimatePresence mode="popLayout">
              {universityNames.map((name) => (
                <motion.span
                  key={name}
                  layout
                  initial={{ opacity: 0, scale: 0.8 }}
                  animate={{ opacity: 1, scale: 1 }}
                  exit={{ opacity: 0, scale: 0.8 }}
                  className="mb-2 mr-2 inline-flex max-w-full items-center gap-1 rounded-full bg-purple-100 px-3 py-1 text-sm font-medium text-purple-700"
                >
                  <span className="truncate">{name}</span>
                  <button
                    type="button"
                    onClick={() => removeUniversity(name)}
                    className="ml-0.5 rounded-full p-0.5 hover:bg-purple-200"
                  >
                    <X className="h-3 w-3" />
                  </button>
                </motion.span>
              ))}
            </AnimatePresence>

            {/* Quick picks */}
            {universityNames.length === 0 && (
              <div className="mt-2 flex flex-wrap gap-1.5">
                {POPULAR_UNIVERSITIES.slice(0, 8).map((name) => (
                  <button
                    key={name}
                    onClick={() => addUniversity(name)}
                    className="rounded-full border border-neutral-200 bg-white px-2.5 py-1 text-xs text-neutral-600 transition-colors hover:border-purple-300 hover:bg-purple-50 hover:text-purple-700"
                  >
                    + {name}
                  </button>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Major Input */}
        <div className="mb-6">
          <label className="mb-2 flex items-center gap-2 text-sm font-medium text-neutral-700">
            <BookOpen className="h-4 w-4 text-blue-500" />
            意向专业
            <span className="text-xs text-neutral-400">（可选）</span>
          </label>
          <div className="mb-2 flex flex-col gap-2 sm:flex-row">
            <input
              type="text"
              placeholder="输入专业名称，按 Enter 添加"
              value={majorInput}
              onChange={(e) => setMajorInput(e.target.value)}
              onKeyDown={handleMajorInputKeyDown}
              className="flex-1 rounded-xl border border-neutral-200 bg-white px-4 py-2.5 text-sm shadow-sm outline-none transition-all focus:border-blue-400 focus:ring-2 focus:ring-blue-200"
            />
            <Button
              size="sm"
              variant="outline"
              onClick={() => {
                addMajor(majorInput);
                setMajorInput("");
              }}
              className="h-10 w-full shrink-0 sm:w-auto"
            >
              <Plus className="h-4 w-4" />
            </Button>
          </div>

          {/* Selected majors */}
          <AnimatePresence mode="popLayout">
            {preferredMajors.map((name) => (
              <motion.span
                key={name}
                layout
                initial={{ opacity: 0, scale: 0.8 }}
                animate={{ opacity: 1, scale: 1 }}
                exit={{ opacity: 0, scale: 0.8 }}
                className="mb-2 mr-2 inline-flex max-w-full items-center gap-1 rounded-full bg-blue-100 px-3 py-1 text-sm font-medium text-blue-700"
              >
                <span className="truncate">{name}</span>
                <button
                  type="button"
                  onClick={() => removeMajor(name)}
                  className="ml-0.5 rounded-full p-0.5 hover:bg-blue-200"
                >
                  <X className="h-3 w-3" />
                </button>
              </motion.span>
            ))}
          </AnimatePresence>

          {/* Quick picks */}
          {preferredMajors.length === 0 && (
            <div className="mt-2 flex flex-wrap gap-1.5">
              {POPULAR_MAJORS.slice(0, 6).map((name) => (
                <button
                  key={name}
                  onClick={() => addMajor(name)}
                  className="rounded-full border border-neutral-200 bg-white px-2.5 py-1 text-xs text-neutral-600 transition-colors hover:border-blue-300 hover:bg-blue-50 hover:text-blue-700"
                >
                  + {name}
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="mb-6">
          <label className="mb-2 flex items-center gap-2 text-sm font-medium text-neutral-700">
            <Sparkles className="h-4 w-4 text-sky-500" />
            喜欢的方向
            <span className="text-xs text-neutral-400">
              （推荐模式建议填写）
            </span>
          </label>
          <div className="mb-2 flex flex-col gap-2 sm:flex-row">
            <input
              type="text"
              placeholder="例如：城市发展、计算机、师范、医学、稳定就业"
              value={directionInput}
              onChange={(e) => setDirectionInput(e.target.value)}
              onKeyDown={handleDirectionInputKeyDown}
              className="flex-1 rounded-xl border border-neutral-200 bg-white px-4 py-2.5 text-sm shadow-sm outline-none transition-all focus:border-sky-400 focus:ring-2 focus:ring-sky-200"
            />
            <Button
              size="sm"
              variant="outline"
              onClick={() => {
                addDirection(directionInput);
                setDirectionInput("");
              }}
              className="h-10 w-full shrink-0 sm:w-auto"
            >
              <Plus className="h-4 w-4" />
            </Button>
          </div>

          <AnimatePresence mode="popLayout">
            {preferredDirections.map((name) => (
              <motion.span
                key={name}
                layout
                initial={{ opacity: 0, scale: 0.8 }}
                animate={{ opacity: 1, scale: 1 }}
                exit={{ opacity: 0, scale: 0.8 }}
                className="mb-2 mr-2 inline-flex max-w-full items-center gap-1 rounded-full bg-sky-100 px-3 py-1 text-sm font-medium text-sky-700"
              >
                <span className="truncate">{name}</span>
                <button
                  type="button"
                  onClick={() => removeDirection(name)}
                  className="ml-0.5 rounded-full p-0.5 hover:bg-sky-200"
                >
                  <X className="h-3 w-3" />
                </button>
              </motion.span>
            ))}
          </AnimatePresence>
        </div>

        <div className="mb-6">
          <label className="mb-2 flex items-center gap-2 text-sm font-medium text-neutral-700">
            <Heart className="h-4 w-4 text-rose-500" />
            个人偏好
            <span className="text-xs text-neutral-400">（可选）</span>
          </label>
          <textarea
            rows={3}
            placeholder="例如：想去省会城市、希望以后好就业、家里更希望读师范或医学、能接受转专业竞争等"
            value={personalPreferences}
            onChange={(e) => setPersonalPreferences(e.target.value)}
            className="w-full rounded-2xl border border-neutral-200 bg-white px-4 py-3 text-sm shadow-sm outline-none transition-all focus:border-rose-300 focus:ring-2 focus:ring-rose-100"
          />
        </div>

        {/* Actions */}
        <div className="flex flex-col-reverse gap-3 sm:flex-row">
          <Button
            variant="outline"
            onClick={onCancel}
            className="h-11 w-full flex-1"
          >
            返回
          </Button>
          <Button
            variant="warm"
            onClick={handleConfirm}
            disabled={!canSubmit}
            className="h-11 w-full flex-1"
          >
            <GraduationCap className="mr-2 h-4 w-4" />
            开始分析
          </Button>
        </div>
      </div>
    </motion.div>
  );
}
