import { useCallback, useEffect, useRef, useState } from "react";
import type {
  AgentExecutionStep,
  SchoolAnalysis,
  SchoolRanking,
  SchoolType,
  SearchStatus,
} from "@/types/school";
import { searchSchoolsStream } from "@/lib/api";

const PLACEHOLDER_ANALYSIS: SchoolAnalysis = {
  activityScore: 0,
  lastPostDate: "",
  reputationScore: 0,
  reputationSummary: "",
  contentQualityScore: 0,
  teachingStaffScore: 0,
  teachingStaffDesc: "",
  awardsCount: 0,
  awardsDetail: [],
  studentActivityScore: 0,
  studentActivityDesc: "",
  sources: [],
};

export function useSchoolSearch() {
  const [status, setStatus] = useState<SearchStatus>("idle");
  const [rankings, setRankings] = useState<SchoolRanking[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [locationAddress, setLocationAddress] = useState("");
  const [analyzedCount, setAnalyzedCount] = useState(0);
  const [totalCount, setTotalCount] = useState(0);
  const [agentSteps, setAgentSteps] = useState<AgentExecutionStep[]>([]);

  const abortRef = useRef<AbortController | null>(null);

  const search = useCallback(
    async (
      latitude: number,
      longitude: number,
      schoolType: SchoolType,
      address?: string,
      radius?: number,
      studentScore?: number,
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
      setAgentSteps([]);

      const rankingMap = new Map<string, SchoolRanking>();

      try {
        for await (const event of searchSchoolsStream(
          {
            latitude,
            longitude,
            schoolType,
            radius: radius ?? 5000,
            studentScore,
          },
          controller.signal,
          token,
        )) {
          if (event.type === "meta") {
            setLocationAddress(address ?? event.location.address);
            setTotalCount(event.totalFound);

            if (event.totalFound === 0) {
              setRankings([]);
              continue;
            }

            event.schools.forEach((school, index) => {
              rankingMap.set(school.id, {
                school,
                analysis: PLACEHOLDER_ANALYSIS,
                rank: index + 1,
                overallScore: 0,
                recommendation: "",
                isTopPick: false,
                analyzing: true,
              });
            });
            setRankings(Array.from(rankingMap.values()));
            // 进入流式分析阶段：学校列表已返回，逐条分析进行中
            setStatus("streaming");
          } else if (event.type === "analysis") {
            const existing = rankingMap.get(event.schoolId);
            if (!existing) continue;
            rankingMap.set(event.schoolId, {
              ...existing,
              analysis: event.analysis,
              overallScore: event.overallScore,
              admissionMatchScore: event.admissionMatchScore,
              analyzing: false,
            });
            setRankings(Array.from(rankingMap.values()));
            setAnalyzedCount((count) => count + 1);
          } else if (event.type === "agent_step") {
            setAgentSteps((current) => [...current, event.step]);
          } else if (event.type === "done") {
            if (event.rankings && event.rankings.length > 0) {
              setRankings(event.rankings);
            }
            setStatus("success");
          } else if (event.type === "error") {
            throw new Error(event.message);
          }
        }
      } catch (err) {
        if ((err as { name?: string })?.name === "AbortError") {
          return;
        }
        const message = err instanceof Error ? err.message : "搜索出错，请重试";
        // 后端 401 会返回 "请先登录" 相关消息
        if (message.includes("登录") || message.includes("401")) {
          onAuthError?.();
        }
        setError(message);
        setStatus("error");
      } finally {
        if (abortRef.current === controller) {
          abortRef.current = null;
        }
      }
    },
    [],
  );

  const reset = useCallback(() => {
    abortRef.current?.abort();
    abortRef.current = null;
    setStatus("idle");
    setRankings([]);
    setError(null);
    setLocationAddress("");
    setAnalyzedCount(0);
    setTotalCount(0);
    setAgentSteps([]);
  }, []);

  useEffect(() => {
    return () => {
      abortRef.current?.abort();
    };
  }, []);

  return {
    status,
    setStatus,
    rankings,
    error,
    locationAddress,
    analyzedCount,
    totalCount,
    agentSteps,
    search,
    reset,
  };
}
