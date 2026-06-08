import { useCallback, useState } from "react";
import type { WeightConfig } from "@/lib/scoreCalculator";
import { DEFAULT_WEIGHTS, normalizeWeights } from "@/lib/scoreCalculator";

const STORAGE_KEY = "school-weight-config";

function loadWeights(): WeightConfig {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as Partial<WeightConfig>;
      // 基本校验：7 个 key 都存在且都是 number
      const keys = Object.keys(DEFAULT_WEIGHTS) as (keyof WeightConfig)[];
      const valid = keys.every(
        (k) => typeof parsed[k] === "number" && (parsed[k] as number) >= 0,
      );
      if (valid) return normalizeWeights(parsed as WeightConfig);
    }
  } catch {
    // ignore
  }
  return { ...DEFAULT_WEIGHTS };
}

export function useWeightConfig() {
  const [weights, setWeightsState] = useState<WeightConfig>(loadWeights);

  const setWeights = useCallback((next: WeightConfig) => {
    const normalized = normalizeWeights(next);
    setWeightsState(normalized);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(normalized));
    } catch {
      // ignore
    }
  }, []);

  const resetToDefault = useCallback(() => {
    setWeightsState({ ...DEFAULT_WEIGHTS });
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // ignore
    }
  }, []);

  return { weights, setWeights, resetToDefault };
}
