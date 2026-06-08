import { describe, it, expect, beforeEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useWeightConfig } from "../useWeightConfig";
import { DEFAULT_WEIGHTS } from "@/lib/scoreCalculator";

describe("useWeightConfig", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("should return default weights initially", () => {
    const { result } = renderHook(() => useWeightConfig());
    expect(result.current.weights).toEqual(DEFAULT_WEIGHTS);
  });

  it("should update weights", () => {
    const { result } = renderHook(() => useWeightConfig());
    const newWeights = {
      ...DEFAULT_WEIGHTS,
      teachingStaff: 0.5,
      reputation: 0.1,
      awards: 0.1,
      activity: 0.1,
      contentQuality: 0.1,
      studentActivity: 0.05,
      distance: 0.05,
    };
    act(() => {
      result.current.setWeights(newWeights);
    });
    expect(result.current.weights.teachingStaff).toBeCloseTo(
      newWeights.teachingStaff / 1,
      2,
    );
  });

  it("should persist weights to localStorage", () => {
    const { result } = renderHook(() => useWeightConfig());
    act(() => {
      result.current.setWeights({
        ...DEFAULT_WEIGHTS,
        teachingStaff: 0.4,
      });
    });
    const stored = localStorage.getItem("school-weight-config");
    expect(stored).toBeTruthy();
    const parsed = JSON.parse(stored!);
    expect(parsed.teachingStaff).toBeDefined();
  });

  it("should reset to defaults", () => {
    const { result } = renderHook(() => useWeightConfig());
    act(() => {
      result.current.setWeights({
        ...DEFAULT_WEIGHTS,
        teachingStaff: 0.9,
        reputation: 0.02,
        awards: 0.02,
        activity: 0.02,
        contentQuality: 0.02,
        studentActivity: 0.01,
        distance: 0.01,
      });
    });
    act(() => {
      result.current.resetToDefault();
    });
    expect(result.current.weights).toEqual(DEFAULT_WEIGHTS);
  });
});
