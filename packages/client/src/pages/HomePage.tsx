import { useState, useCallback, useEffect, useRef } from "react";
import { MapPin, RotateCcw, GraduationCap } from "lucide-react";
import { Header } from "@/components/layout/Header";
import { Footer } from "@/components/layout/Footer";
import { AuthDialog } from "@/components/auth/AuthDialog";
import { HeroSection } from "@/components/home/HeroSection";
import { FeaturesSection } from "@/components/home/FeaturesSection";
import { ValueSection } from "@/components/home/ValueSection";
import { SearchHistoryCard } from "@/components/home/SearchHistoryCard";
import { SchoolTypeDialog } from "@/components/search/SchoolTypeDialog";
import { ScoreInputDialog } from "@/components/search/ScoreInputDialog";
import { LocationStep } from "@/components/search/LocationStep";
import { SearchingView } from "@/components/search/SearchingView";
import { UniversitySearchDialog } from "@/components/search/UniversitySearchDialog";
import { SchoolRankingList } from "@/components/results/SchoolRanking";
import { UniversityRankingList } from "@/components/results/UniversityRanking";
import { ComparisonBarChart } from "@/components/results/BarChart";
import { StepIndicator } from "@/components/shared/StepIndicator";
import { ShareButton } from "@/components/shared/ShareButton";
import { MessageBoard } from "@/components/messages/MessageBoard";
import { Button } from "@/components/ui/button";
import { geocodeAddress, trackSiteVisit } from "@/lib/api";
import { useAuth } from "@/hooks/useAuth";
import { useGeolocation } from "@/hooks/useGeolocation";
import { useSchoolSearch } from "@/hooks/useSchoolSearch";
import { useSearchHistory } from "@/hooks/useSearchHistory";
import { useUniversitySearch } from "@/hooks/useUniversitySearch";
import type { SearchHistoryItem } from "@/hooks/useSearchHistory";
import type { SchoolType } from "@/types/school";
import { SchoolTypeLabels } from "@/types/school";

/** 从 URL 查询参数中解析搜索状态 */
function parseSearchParamsFromURL(): {
  lat: number;
  lng: number;
  type: SchoolType;
  radius: number;
  studentScore?: number;
} | null {
  const params = new URLSearchParams(window.location.search);
  const lat = params.get("lat");
  const lng = params.get("lng");
  const type = params.get("type") as SchoolType | null;
  const radius = params.get("r");
  const score = params.get("score");
  if (
    lat &&
    lng &&
    type &&
    ["kindergarten", "primary", "junior_high", "senior_high"].includes(type)
  ) {
    return {
      lat: parseFloat(lat),
      lng: parseFloat(lng),
      type,
      radius: radius ? parseInt(radius, 10) : 5000,
      studentScore: score ? parseInt(score, 10) : undefined,
    };
  }
  return null;
}

/** 将搜索参数写入 URL（不触发页面刷新） */
function updateSearchParamsInURL(
  lat: number,
  lng: number,
  type: SchoolType,
  radius: number,
  studentScore?: number,
) {
  const params = new URLSearchParams();
  params.set("lat", lat.toFixed(6));
  params.set("lng", lng.toFixed(6));
  params.set("type", type);
  params.set("r", String(radius));
  if (studentScore) {
    params.set("score", String(studentScore));
  }
  const newUrl = `${window.location.pathname}?${params.toString()}`;
  window.history.replaceState(null, "", newUrl);
}

/** 清除 URL 中的搜索参数 */
function clearSearchParamsInURL() {
  window.history.replaceState(null, "", window.location.pathname);
}

const STEPS = ["选择类型", "获取位置", "搜索分析", "查看结果"];
const UNI_STEPS = ["填写信息", "AI 分析", "查看排名"];

type SearchMode = "school" | "university";

export function HomePage() {
  const [showTypeDialog, setShowTypeDialog] = useState(false);
  const [selectedType, setSelectedType] = useState<SchoolType | null>(null);
  const [selectedRadius, setSelectedRadius] = useState(5000);
  const [studentScore, setStudentScore] = useState<number | undefined>(
    undefined,
  );
  const [showScoreDialog, setShowScoreDialog] = useState(false);
  const [pendingType, setPendingType] = useState<SchoolType | null>(null);
  const [currentStep, setCurrentStep] = useState(-1);
  const [authDialogOpen, setAuthDialogOpen] = useState(false);

  // 大学报考模式
  const [searchMode, setSearchMode] = useState<SearchMode>("school");
  const [uniStudentScore, setUniStudentScore] = useState<number>(0);
  const [uniStep, setUniStep] = useState(-1); // -1=not started, 0=input, 1=analyzing, 2=done

  const auth = useAuth();

  const {
    location,
    status: geoStatus,
    error: geoError,
    requestLocation,
    setManualLocation,
  } = useGeolocation();

  const {
    status: searchStatus,
    rankings,
    error: searchError,
    locationAddress,
    analyzedCount,
    totalCount,
    search,
    reset,
  } = useSchoolSearch();

  const {
    status: uniStatus,
    rankings: uniRankings,
    error: uniError,
    analyzedCount: uniAnalyzedCount,
    totalCount: uniTotalCount,
    analysesMap: uniAnalysesMap,
    search: uniSearch,
    reset: uniReset,
  } = useUniversitySearch();

  const handleStart = useCallback(() => {
    setShowTypeDialog(true);
  }, []);

  const { history, addHistory, removeHistory, clearHistory } =
    useSearchHistory();

  const searchCoordsRef = useRef<{
    latitude: number;
    longitude: number;
  } | null>(null);

  useEffect(() => {
    void trackSiteVisit(window.location.pathname);
  }, []);

  // 从 URL 恢复搜索状态（分享链接打开时）
  const restoredFromURL = useRef(false);
  useEffect(() => {
    if (restoredFromURL.current) return;
    const urlParams = parseSearchParamsFromURL();
    if (urlParams) {
      restoredFromURL.current = true;
      setSelectedType(urlParams.type);
      setSelectedRadius(urlParams.radius);
      setStudentScore(urlParams.studentScore);
      setCurrentStep(2);
      searchCoordsRef.current = {
        latitude: urlParams.lat,
        longitude: urlParams.lng,
      };
      search(
        urlParams.lat,
        urlParams.lng,
        urlParams.type,
        undefined,
        urlParams.radius,
        urlParams.studentScore,
      );
    }
  }, [search]);

  const handleTypeSelect = useCallback(
    (type: SchoolType, radius: number) => {
      setSelectedRadius(radius);
      setShowTypeDialog(false);

      if (type === "senior_high") {
        setPendingType(type);
        setShowScoreDialog(true);
      } else {
        setSelectedType(type);
        setCurrentStep(1);
        requestLocation();
      }
    },
    [requestLocation],
  );

  const handleScoreConfirm = useCallback(
    (score: number) => {
      setStudentScore(score);
      setShowScoreDialog(false);
      if (pendingType) {
        setSelectedType(pendingType);
        setPendingType(null);
        setCurrentStep(1);
        requestLocation();
      }
    },
    [pendingType, requestLocation],
  );

  const handleScoreSkip = useCallback(() => {
    setShowScoreDialog(false);
    if (pendingType) {
      setSelectedType(pendingType);
      setPendingType(null);
      setCurrentStep(1);
      requestLocation();
    }
  }, [pendingType, requestLocation]);

  const handleLocationSuccess = useCallback(
    (lat: number, lng: number, address?: string) => {
      if (selectedType) {
        searchCoordsRef.current = { latitude: lat, longitude: lng };
        setCurrentStep(2);
        search(lat, lng, selectedType, address, selectedRadius, studentScore);
      }
    },
    [selectedType, search, selectedRadius, studentScore],
  );

  const handleManualInput = useCallback(
    async (address: string) => {
      if (!selectedType) return;

      try {
        const geo = await geocodeAddress(address);
        searchCoordsRef.current = {
          latitude: geo.latitude,
          longitude: geo.longitude,
        };
        setManualLocation({
          latitude: geo.latitude,
          longitude: geo.longitude,
          address: geo.address || address,
        });
        setCurrentStep(2);
        search(
          geo.latitude,
          geo.longitude,
          selectedType,
          geo.address || address,
          selectedRadius,
          studentScore,
        );
      } catch {
        searchCoordsRef.current = { latitude: 39.9042, longitude: 116.4074 };
        setManualLocation({ latitude: 39.9042, longitude: 116.4074, address });
        setCurrentStep(2);
        search(
          39.9042,
          116.4074,
          selectedType,
          address,
          selectedRadius,
          studentScore,
        );
      }
    },
    [selectedType, search, setManualLocation, selectedRadius, studentScore],
  );

  const prevGeoStatusRef = useRef<string>("idle");

  useEffect(() => {
    if (
      geoStatus === "success" &&
      prevGeoStatusRef.current !== "success" &&
      currentStep === 1 &&
      location
    ) {
      prevGeoStatusRef.current = "success";
      const timer = setTimeout(() => {
        handleLocationSuccess(
          location.latitude,
          location.longitude,
          location.address,
        );
      }, 800);

      return () => clearTimeout(timer);
    }

    if (geoStatus !== prevGeoStatusRef.current) {
      prevGeoStatusRef.current = geoStatus;
    }
  }, [geoStatus, currentStep, location, handleLocationSuccess]);

  useEffect(() => {
    if (
      (searchStatus === "streaming" || searchStatus === "success") &&
      currentStep === 2
    ) {
      setCurrentStep(3);
    }
  }, [searchStatus, currentStep]);

  // 搜索进入流式阶段时记录历史 + 更新 URL
  const hasRecordedRef = useRef(false);
  useEffect(() => {
    if (
      searchStatus === "streaming" &&
      selectedType &&
      searchCoordsRef.current &&
      !hasRecordedRef.current
    ) {
      hasRecordedRef.current = true;
      addHistory({
        schoolType: selectedType,
        radius: selectedRadius,
        address: locationAddress || "未知地址",
        latitude: searchCoordsRef.current.latitude,
        longitude: searchCoordsRef.current.longitude,
        totalCount,
        studentScore: studentScore ?? undefined,
      });
      // 更新 URL 便于分享
      updateSearchParamsInURL(
        searchCoordsRef.current.latitude,
        searchCoordsRef.current.longitude,
        selectedType,
        selectedRadius,
        studentScore,
      );
    }
    if (searchStatus === "idle") {
      hasRecordedRef.current = false;
    }
  }, [
    searchStatus,
    selectedType,
    selectedRadius,
    studentScore,
    locationAddress,
    totalCount,
    addHistory,
  ]);

  const handleReset = useCallback(() => {
    reset();
    setSelectedType(null);
    setSelectedRadius(5000);
    setStudentScore(undefined);
    setPendingType(null);
    setShowScoreDialog(false);
    setCurrentStep(-1);
    prevGeoStatusRef.current = "idle";
    restoredFromURL.current = false;
    clearSearchParamsInURL();
  }, [reset]);

  const handleReplayHistory = useCallback(
    (item: SearchHistoryItem) => {
      setSelectedType(item.schoolType);
      setSelectedRadius(item.radius);
      setStudentScore(item.studentScore);
      setCurrentStep(2);
      searchCoordsRef.current = {
        latitude: item.latitude,
        longitude: item.longitude,
      };
      search(
        item.latitude,
        item.longitude,
        item.schoolType,
        item.address,
        item.radius,
        item.studentScore,
      );
    },
    [search],
  );

  // ─── 大学报考模式处理 ───────────────────────────────────

  const handleStartUniversity = useCallback(() => {
    setSearchMode("university");
    setUniStep(0);
  }, []);

  const handleUniversityConfirm = useCallback(
    (
      score: number,
      universityNames: string[],
      preferredMajors: string[],
    ) => {
      setUniStudentScore(score);
      setUniStep(1);
      uniSearch(
        score,
        universityNames,
        preferredMajors.length > 0 ? preferredMajors : undefined,
      );
    },
    [uniSearch],
  );

  const handleUniversityCancel = useCallback(() => {
    uniReset();
    setSearchMode("school");
    setUniStep(-1);
  }, [uniReset]);

  const handleUniversityReset = useCallback(() => {
    uniReset();
    setUniStep(0);
  }, [uniReset]);

  // 大学分析完成后自动切换步骤
  useEffect(() => {
    if (uniStatus === "success" || uniStatus === "error") {
      setUniStep(2);
    }
  }, [uniStatus]);

  const isInFlow = currentStep >= 0;
  const isInUniFlow = uniStep >= 0;

  return (
    <div className="min-h-screen bg-background text-foreground">
      <Header
        user={auth.user}
        onAuthClick={() => setAuthDialogOpen(true)}
        onLogout={auth.logout}
      />

      <main className="pb-10 pt-24 sm:pt-28">
        {!isInFlow && !isInUniFlow && (
          <>
            <HeroSection
              onStart={handleStart}
              onStartUniversity={handleStartUniversity}
            />
            <SearchHistoryCard
              history={history}
              onReplay={handleReplayHistory}
              onRemove={removeHistory}
              onClear={clearHistory}
            />
            <FeaturesSection />
            <ValueSection />
          </>
        )}

        {/* ─── 学校搜索流程 ─── */}
        {isInFlow && searchMode === "school" && (
          <section className="px-4 py-6 sm:px-6 lg:px-8">
            <div className="mx-auto max-w-6xl space-y-6">
              <div className="grid gap-4 lg:grid-cols-[0.9fr_1.1fr] lg:items-end">
                <div>
                  <p className="text-sm font-medium uppercase tracking-[0.24em] text-warm-600">
                    Search Flow
                  </p>
                  <h1 className="mt-3 font-display text-3xl font-semibold text-neutral-900 sm:text-4xl">
                    {selectedType
                      ? `${SchoolTypeLabels[selectedType]}择校流程`
                      : "择校流程"}
                  </h1>
                  <p className="mt-3 max-w-2xl text-sm leading-7 text-neutral-600 sm:text-base">
                    保持原有搜索逻辑不变，重新组织为更清晰的步骤式界面，让当前位置、分析状态与结果展示更容易理解。
                  </p>
                </div>

                <div className="flex flex-wrap items-center gap-3 lg:justify-end">
                  {locationAddress && (
                    <div className="inline-flex items-center gap-2 rounded-full bg-background/80 px-4 py-2 text-sm text-neutral-600 shadow-xs">
                      <MapPin className="h-4 w-4 text-warm-500" />
                      <span className="max-w-60 truncate">
                        {locationAddress}
                      </span>
                    </div>
                  )}
                  {currentStep === 3 && (
                    <ShareButton className="text-neutral-500 hover:text-warm-600 hover:bg-warm-50" />
                  )}
                  <Button onClick={handleReset} variant="outline">
                    <RotateCcw className="h-4 w-4" />
                    重新开始
                  </Button>
                </div>
              </div>

              {currentStep < 3 && (
                <StepIndicator
                  steps={STEPS}
                  currentStep={currentStep}
                  className="mb-2"
                />
              )}

              {currentStep === 1 && (
                <LocationStep
                  status={geoStatus}
                  error={geoError}
                  onRetry={requestLocation}
                  onManualInput={handleManualInput}
                />
              )}

              {currentStep === 2 && selectedType && (
                <SearchingView schoolType={selectedType} />
              )}

              {currentStep === 3 && selectedType && (
                <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_320px]">
                  <SchoolRankingList
                    rankings={rankings}
                    schoolType={selectedType}
                    locationAddress={locationAddress}
                    analyzedCount={analyzedCount}
                    totalCount={totalCount}
                    onReset={handleReset}
                  />

                  {rankings.length > 1 && (
                    <aside className="surface-elevated h-fit rounded-4xl p-5 sm:p-6 xl:sticky xl:top-28">
                      <ComparisonBarChart rankings={rankings} />
                    </aside>
                  )}

                  <div className="xl:col-span-2">
                    <MessageBoard
                      rankings={rankings}
                      token={auth.token}
                      user={auth.user}
                      onRequireAuth={() => setAuthDialogOpen(true)}
                    />
                  </div>
                </div>
              )}

              {searchStatus === "error" && (
                <div className="surface-elevated rounded-4xl px-6 py-12 text-center">
                  <p className="text-base text-warm-700">{searchError}</p>
                  <Button onClick={handleReset} variant="warm" className="mt-5">
                    重新开始
                  </Button>
                </div>
              )}
            </div>
          </section>
        )}

        {/* ─── 大学报考分析流程 ─── */}
        {isInUniFlow && searchMode === "university" && (
          <section className="px-4 py-6 sm:px-6 lg:px-8">
            <div className="mx-auto max-w-4xl space-y-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium uppercase tracking-[0.24em] text-purple-600">
                    University Analysis
                  </p>
                  <h1 className="mt-3 font-display text-3xl font-semibold text-neutral-900 sm:text-4xl">
                    大学报考分析
                  </h1>
                </div>
                {uniStep === 0 && (
                  <Button
                    onClick={handleUniversityCancel}
                    variant="outline"
                    size="sm"
                  >
                    <RotateCcw className="mr-1.5 h-3.5 w-3.5" />
                    返回
                  </Button>
                )}
              </div>

              <StepIndicator
                steps={UNI_STEPS}
                currentStep={uniStep}
                className="mb-2"
              />

              {uniStep === 0 && (
                <UniversitySearchDialog
                  onConfirm={handleUniversityConfirm}
                  onCancel={handleUniversityCancel}
                />
              )}

              {uniStep === 1 && (
                <div className="flex flex-col items-center gap-4 py-12">
                  <div className="flex h-16 w-16 items-center justify-center rounded-full bg-purple-100">
                    <GraduationCap className="h-8 w-8 animate-pulse text-purple-600" />
                  </div>
                  <p className="text-lg font-medium text-neutral-700">
                    AI 正在分析大学录取数据...
                  </p>
                  <p className="text-sm text-neutral-500">
                    已分析 {uniAnalyzedCount}/{uniTotalCount} 所大学
                  </p>
                </div>
              )}

              {uniStep === 2 && (
                <UniversityRankingList
                  rankings={uniRankings}
                  analysesMap={uniAnalysesMap}
                  analyzedCount={uniAnalyzedCount}
                  totalCount={uniTotalCount}
                  onReset={handleUniversityReset}
                />
              )}

              {uniStatus === "error" && uniStep === 2 && uniError && (
                <div className="surface-elevated rounded-4xl px-6 py-12 text-center">
                  <p className="text-base text-warm-700">{uniError}</p>
                  <Button
                    onClick={handleUniversityReset}
                    variant="warm"
                    className="mt-5"
                  >
                    重新搜索
                  </Button>
                </div>
              )}
            </div>
          </section>
        )}
      </main>

      <SchoolTypeDialog
        open={showTypeDialog}
        onClose={() => setShowTypeDialog(false)}
        onSelect={handleTypeSelect}
      />

      <ScoreInputDialog
        open={showScoreDialog}
        onClose={handleScoreSkip}
        onConfirm={handleScoreConfirm}
      />

      <AuthDialog
        open={authDialogOpen}
        onOpenChange={setAuthDialogOpen}
        onLogin={auth.login}
        onRegister={auth.register}
      />

      {!isInFlow && !isInUniFlow && <Footer />}
    </div>
  );
}
