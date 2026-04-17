import { useState, useCallback } from "react";
import type { SchoolRanking, SchoolType, SearchStatus } from "@/types/school";
import { searchSchools } from "@/lib/api";

export function useSchoolSearch() {
  const [status, setStatus] = useState<SearchStatus>("idle");
  const [rankings, setRankings] = useState<SchoolRanking[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [locationAddress, setLocationAddress] = useState("");

  const search = useCallback(
    async (
      latitude: number,
      longitude: number,
      schoolType: SchoolType,
      address?: string,
    ) => {
      setStatus("searching");
      setError(null);

      try {
        const response = await searchSchools({
          latitude,
          longitude,
          schoolType,
          radius: 5000,
        });

        if (response.success) {
          setRankings(response.data.rankings);
          setLocationAddress(address ?? response.data.location.address);
          setStatus("success");
        } else {
          throw new Error(response.message || "搜索失败");
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : "搜索出错，请重试");
        setStatus("error");
      }
    },
    [],
  );

  const reset = useCallback(() => {
    setStatus("idle");
    setRankings([]);
    setError(null);
    setLocationAddress("");
  }, []);

  return {
    status,
    setStatus,
    rankings,
    error,
    locationAddress,
    search,
    reset,
  };
}
