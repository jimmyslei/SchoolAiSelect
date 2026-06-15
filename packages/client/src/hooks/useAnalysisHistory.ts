import { useCallback, useEffect, useRef, useState } from "react";
import {
  deleteAllAnalysisHistory,
  deleteAnalysisHistory,
  getAnalysisHistory,
  getAnalysisHistoryDetail,
  type AnalysisHistoryDetail,
  type AnalysisHistoryItem,
} from "@/lib/api";

interface UseAnalysisHistoryOptions {
  token?: string;
  pageSize?: number;
}

export function useAnalysisHistory({
  token,
  pageSize = 20,
}: UseAnalysisHistoryOptions) {
  const [items, setItems] = useState<AnalysisHistoryItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // 用于取消进行中的 fetch
  const abortRef = useRef<AbortController | null>(null);

  const load = useCallback(
    async (p: number = 1) => {
      if (!token) return;
      abortRef.current?.abort();
      const ctrl = new AbortController();
      abortRef.current = ctrl;

      setLoading(true);
      setError(null);
      try {
        const result = await getAnalysisHistory(token, p, pageSize);
        if (!ctrl.signal.aborted) {
          setItems(result.items);
          setTotal(result.total);
          setPage(result.page);
        }
      } catch (err) {
        if (!ctrl.signal.aborted) {
          setError(err instanceof Error ? err.message : "加载失败");
        }
      } finally {
        if (!ctrl.signal.aborted) {
          setLoading(false);
        }
      }
    },
    [token, pageSize],
  );

  // token 变化时自动加载第一页
  useEffect(() => {
    if (token) {
      load(1);
    } else {
      setItems([]);
      setTotal(0);
      setPage(1);
    }
    return () => {
      abortRef.current?.abort();
    };
  }, [token, load]);

  const refresh = useCallback(() => load(page), [load, page]);

  const loadPage = useCallback((p: number) => load(p), [load]);

  const getDetail = useCallback(
    async (id: number): Promise<AnalysisHistoryDetail> => {
      if (!token) throw new Error("未登录");
      return getAnalysisHistoryDetail(id, token);
    },
    [token],
  );

  const deleteItem = useCallback(
    async (id: number) => {
      if (!token) return;
      await deleteAnalysisHistory(id, token);
      setItems((prev) => prev.filter((it) => it.id !== id));
      setTotal((prev) => Math.max(0, prev - 1));
    },
    [token],
  );

  const deleteAll = useCallback(async () => {
    if (!token) return;
    await deleteAllAnalysisHistory(token);
    setItems([]);
    setTotal(0);
    setPage(1);
  }, [token]);

  return {
    items,
    total,
    page,
    pageSize,
    loading,
    error,
    refresh,
    loadPage,
    getDetail,
    deleteItem,
    deleteAll,
  };
}
