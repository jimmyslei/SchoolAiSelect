import { useCallback, useRef, useState } from "react";
import type {
  AgentExecutionStep,
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
  const [agentSteps, setAgentSteps] = useState<AgentExecutionStep[]>([]);

  const abortRef = useRef<AbortController | null>(null);

  const search = useCallback(
    async (
      score: number,
      selectedProvince: string,
      token?: string,
      onAuthError?: () => void,
      selectedCity?: string,
    ) => {
      abortRef.current?.abort();
      const controller = new AbortController();
      abortRef.current = controller;

      setStatus("searching");
      setError(null);
      setRecommendations([]);
      setProvince(selectedProvince);
      setStudentScore(score);
      setAgentSteps([]);

      try {
        for await (const event of recommendHighSchoolsStream(
          {
            studentScore: score,
            province: selectedProvince,
            city: selectedCity,
          },
          controller.signal,
          token,
        )) {
          switch (event.type) {
            case "meta":
              setStatus("streaming");
              break;

            case "agent_step":
              setAgentSteps((current) => [...current, event.step]);
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

        setStatus((current) => (current === "streaming" ? "success" : current));
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
    setAgentSteps([]);
  }, []);

  return {
    status,
    recommendations,
    error,
    province,
    studentScore,
    agentSteps,
    search,
    reset,
  };
}
