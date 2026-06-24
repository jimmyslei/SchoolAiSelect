import { useCallback, useRef, useState } from "react";
import type {
  AgentExecutionStep,
  UniversityAnalysis,
  UniversityRanking,
  UniversitySearchStatus,
} from "@/types/school";
import { searchUniversitiesStream } from "@/lib/api";

export function useUniversitySearch() {
  const [status, setStatus] = useState<UniversitySearchStatus>("idle");
  const [rankings, setRankings] = useState<UniversityRanking[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [analyzedCount, setAnalyzedCount] = useState(0);
  const [totalCount, setTotalCount] = useState(0);
  const [analysesMap, setAnalysesMap] = useState<
    Map<string, UniversityAnalysis>
  >(new Map());
  const [agentSteps, setAgentSteps] = useState<AgentExecutionStep[]>([]);

  const abortRef = useRef<AbortController | null>(null);

  const search = useCallback(
    async (
      studentScore: number,
      universityNames: string[],
      preferredMajors?: string[],
      token?: string,
      onAuthError?: () => void,
    ) => {
      abortRef.current?.abort();
      const controller = new AbortController();
      abortRef.current = controller;

      setStatus("searching");
      setError(null);
      setRankings([]);
      setAnalyzedCount(0);
      setTotalCount(0);
      setAnalysesMap(new Map());
      setAgentSteps([]);

      const analysisBuffer = new Map<string, UniversityAnalysis>();

      try {
        for await (const event of searchUniversitiesStream(
          {
            studentScore,
            universityNames,
            preferredMajors,
          },
          controller.signal,
          token,
        )) {
          switch (event.type) {
            case "meta":
              setStatus("streaming");
              setTotalCount(event.totalCount);
              break;

            case "analysis":
              analysisBuffer.set(event.universityName, event.analysis);
              setAnalysesMap(new Map(analysisBuffer));
              setAnalyzedCount((prev) => prev + 1);
              break;

            case "agent_step":
              setAgentSteps((current) => [...current, event.step]);
              break;

            case "done":
              setRankings(event.rankings);
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
          err instanceof Error ? err.message : "搜索过程中发生未知错误";
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
    setRankings([]);
    setError(null);
    setAnalyzedCount(0);
    setTotalCount(0);
    setAnalysesMap(new Map());
    setAgentSteps([]);
  }, []);

  return {
    status,
    rankings,
    error,
    analyzedCount,
    totalCount,
    analysesMap,
    agentSteps,
    search,
    reset,
  };
}
