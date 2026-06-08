import { useCallback } from "react";
import { useLocalStorage } from "./useLocalStorage";
import type { SchoolType } from "@/types/school";

export interface SearchHistoryItem {
  id: string;
  timestamp: number;
  schoolType: SchoolType;
  radius: number;
  address: string;
  totalCount: number;
  /** 用于回放的坐标 */
  latitude: number;
  longitude: number;
  /** 高中学生分数（仅 senior_high 时存在） */
  studentScore?: number;
}

const STORAGE_KEY = "school-search-history";
const MAX_ITEMS = 10;

export function useSearchHistory() {
  const [history, setHistory] = useLocalStorage<SearchHistoryItem[]>(
    STORAGE_KEY,
    [],
  );

  const addHistory = useCallback(
    (item: Omit<SearchHistoryItem, "id" | "timestamp">) => {
      setHistory((prev) => {
        const newItem: SearchHistoryItem = {
          ...item,
          id: crypto.randomUUID(),
          timestamp: Date.now(),
        };
        return [newItem, ...prev].slice(0, MAX_ITEMS);
      });
    },
    [setHistory],
  );

  const removeHistory = useCallback(
    (id: string) => {
      setHistory((prev) => prev.filter((item) => item.id !== id));
    },
    [setHistory],
  );

  const clearHistory = useCallback(() => {
    setHistory([]);
  }, [setHistory]);

  return { history, addHistory, removeHistory, clearHistory };
}
