import { useState, useCallback } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { Plus, X, GraduationCap, BookOpen, Target } from "lucide-react";
import { Button } from "@/components/ui/button";

interface UniversitySearchDialogProps {
  onConfirm: (
    studentScore: number,
    universityNames: string[],
    preferredMajors: string[],
  ) => void;
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
  const [universityNames, setUniversityNames] = useState<string[]>([]);
  const [preferredMajors, setPreferredMajors] = useState<string[]>([]);
  const [uniInput, setUniInput] = useState("");
  const [majorInput, setMajorInput] = useState("");

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

  const score = parseInt(studentScore, 10);
  const canSubmit =
    !isNaN(score) && score >= 100 && score <= 750 && universityNames.length > 0;

  const handleConfirm = useCallback(() => {
    if (canSubmit) {
      onConfirm(score, universityNames, preferredMajors);
    }
  }, [canSubmit, score, universityNames, preferredMajors, onConfirm]);

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      className="mx-auto max-w-2xl"
    >
      <div className="rounded-2xl border border-warm-200/60 bg-white/90 p-6 shadow-lg backdrop-blur-sm sm:p-8">
        {/* Header */}
        <div className="mb-6 flex items-center gap-3">
          <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br from-purple-100 to-blue-100">
            <GraduationCap className="h-6 w-6 text-purple-600" />
          </div>
          <div>
            <h2 className="font-display text-xl font-semibold text-neutral-900">
              大学报考分析
            </h2>
            <p className="text-sm text-neutral-500">
              输入你的成绩和意向大学，AI 为你分析录取概率
            </p>
          </div>
        </div>

        {/* Score Input */}
        <div className="mb-5">
          <label className="mb-2 flex items-center gap-2 text-sm font-medium text-neutral-700">
            <Target className="h-4 w-4 text-warm-500" />
            摸底高考成绩
          </label>
          <div className="flex items-center gap-3">
            <input
              type="number"
              min={100}
              max={750}
              placeholder="输入分数，如 580"
              value={studentScore}
              onChange={(e) => setStudentScore(e.target.value)}
              className="w-full rounded-xl border border-neutral-200 bg-white px-4 py-3 text-lg font-medium text-neutral-900 shadow-sm outline-none transition-all focus:border-warm-400 focus:ring-2 focus:ring-warm-200"
            />
            <span className="shrink-0 text-sm text-neutral-400">/ 750</span>
          </div>
        </div>

        {/* University Input */}
        <div className="mb-5">
          <label className="mb-2 flex items-center gap-2 text-sm font-medium text-neutral-700">
            <GraduationCap className="h-4 w-4 text-purple-500" />
            意向大学
            <span className="text-xs text-neutral-400">（最多 10 所）</span>
          </label>
          <div className="mb-2 flex gap-2">
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
              className="shrink-0"
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
                className="mr-2 mb-2 inline-flex items-center gap-1 rounded-full bg-purple-100 px-3 py-1 text-sm font-medium text-purple-700"
              >
                {name}
                <button
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

        {/* Major Input */}
        <div className="mb-6">
          <label className="mb-2 flex items-center gap-2 text-sm font-medium text-neutral-700">
            <BookOpen className="h-4 w-4 text-blue-500" />
            意向专业
            <span className="text-xs text-neutral-400">（可选）</span>
          </label>
          <div className="mb-2 flex gap-2">
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
              className="shrink-0"
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
                className="mr-2 mb-2 inline-flex items-center gap-1 rounded-full bg-blue-100 px-3 py-1 text-sm font-medium text-blue-700"
              >
                {name}
                <button
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
            <GraduationCap className="mr-2 h-4 w-4" />
            开始分析
          </Button>
        </div>
      </div>
    </motion.div>
  );
}
