import { useCallback, useRef, useState } from "react";
import type {
  HighSchoolRecommendation,
  HighSchoolRecommendStatus,
} from "@/types/school";
import { recommendHighSchoolsStream } from "@/lib/api";

export function useHighSchoolRecommend() {
  const [status, setStatus] = useState<HighSchoolRecommendStatus>("idle");
  const [recommendations, setRecommendations] = useState<
    HighSchoolRecommendation[]
  >([]);
  const [error, setError] = useState<string | null>(null);
  const [province, setProvince] = useState("");
  const [studentScore, setStudentScore] = useState(0);

  const abortRef = useRef<AbortController | null>(null);

  const search = useCallback(
    async (
      score: number,
      selectedProvince: string,
      token?: string,
      onAuthError?: () => void,
    ) => {
      abortRef.current?.abort();
      const controller = new AbortController();
      abortRef.current = controller;

      setStatus("searching");
      setError(null);
      setRecommendations([]);
      setProvince(selectedProvince);
      setStudentScore(score);

      try {
        for await (const event of recommendHighSchoolsStream(
          { studentScore: score, province: selectedProvince },
          controller.signal,
          token,
        )) {
          switch (event.type) {
            case "meta":
              setStatus("streaming");
              break;

            case "done":
              setRecommendations(event.recommendations);
              if (event.recommendations.length === 0) {
                setError(event.message || "暂未找到有效的推荐数据");
              }
              setStatus("success");
              break;

            case "error":
              setError(event.message);
              setStatus("error");
              break;
          }
        }

        if (status === "streaming") {
          setStatus("success");
        }
      } catch (err) {
        if (controller.signal.aborted) return;
        const message =
          err instanceof Error ? err.message : "推荐过程中发生未知错误";
        if (message.includes("登录") || message.includes("401")) {
          onAuthError?.();
        }
        setError(message);
        setStatus("error");
      }
    },
    [],
  );

  const reset = useCallback(() => {
    abortRef.current?.abort();
    setStatus("idle");
    setRecommendations([]);
    setError(null);
    setProvince("");
    setStudentScore(0);
  }, []);

  return {
    status,
    recommendations,
    error,
    province,
    studentScore,
    search,
    reset,
  };
}
