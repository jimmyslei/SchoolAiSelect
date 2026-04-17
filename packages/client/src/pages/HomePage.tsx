import { useState, useCallback, useEffect, useRef } from "react";
import { MapPin, RotateCcw } from "lucide-react";
import { Header } from "@/components/layout/Header";
import { Footer } from "@/components/layout/Footer";
import { HeroSection } from "@/components/home/HeroSection";
import { FeaturesSection } from "@/components/home/FeaturesSection";
import { ValueSection } from "@/components/home/ValueSection";
import { SchoolTypeDialog } from "@/components/search/SchoolTypeDialog";
import { LocationStep } from "@/components/search/LocationStep";
import { SearchingView } from "@/components/search/SearchingView";
import { SchoolRankingList } from "@/components/results/SchoolRanking";
import { ComparisonBarChart } from "@/components/results/BarChart";
import { StepIndicator } from "@/components/shared/StepIndicator";
import { Button } from "@/components/ui/button";
import { geocodeAddress } from "@/lib/api";
import { useGeolocation } from "@/hooks/useGeolocation";
import { useSchoolSearch } from "@/hooks/useSchoolSearch";
import type { SchoolType } from "@/types/school";
import { SchoolTypeLabels } from "@/types/school";

const STEPS = ["选择类型", "获取位置", "搜索分析", "查看结果"];

export function HomePage() {
  const [showTypeDialog, setShowTypeDialog] = useState(false);
  const [selectedType, setSelectedType] = useState<SchoolType | null>(null);
  const [currentStep, setCurrentStep] = useState(-1);

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
    search,
    reset,
  } = useSchoolSearch();

  const handleStart = useCallback(() => {
    setShowTypeDialog(true);
  }, []);

  const handleTypeSelect = useCallback(
    (type: SchoolType) => {
      setSelectedType(type);
      setShowTypeDialog(false);
      setCurrentStep(1);
      requestLocation();
    },
    [requestLocation],
  );

  const handleLocationSuccess = useCallback(
    (lat: number, lng: number, address?: string) => {
      if (selectedType) {
        setCurrentStep(2);
        search(lat, lng, selectedType, address);
      }
    },
    [selectedType, search],
  );

  const handleManualInput = useCallback(
    async (address: string) => {
      if (!selectedType) return;

      try {
        const geo = await geocodeAddress(address);
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
        );
      } catch {
        setManualLocation({ latitude: 39.9042, longitude: 116.4074, address });
        setCurrentStep(2);
        search(39.9042, 116.4074, selectedType, address);
      }
    },
    [selectedType, search, setManualLocation],
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
    if (searchStatus === "success" && currentStep === 2) {
      setCurrentStep(3);
    }
  }, [searchStatus, currentStep]);

  const handleReset = useCallback(() => {
    reset();
    setSelectedType(null);
    setCurrentStep(-1);
    prevGeoStatusRef.current = "idle";
  }, [reset]);

  const isInFlow = currentStep >= 0;

  return (
    <div className="min-h-screen bg-background text-foreground">
      <Header />

      <main className="pb-10 pt-24 sm:pt-28">
        {!isInFlow && (
          <>
            <HeroSection onStart={handleStart} />
            <FeaturesSection />
            <ValueSection />
          </>
        )}

        {isInFlow && (
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
                      <span className="max-w-[240px] truncate">
                        {locationAddress}
                      </span>
                    </div>
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
                    onReset={handleReset}
                  />

                  {rankings.length > 1 && (
                    <aside className="surface-elevated h-fit rounded-[32px] p-5 sm:p-6 xl:sticky xl:top-28">
                      <ComparisonBarChart rankings={rankings} />
                    </aside>
                  )}
                </div>
              )}

              {searchStatus === "error" && (
                <div className="surface-elevated rounded-[32px] px-6 py-12 text-center">
                  <p className="text-base text-warm-700">{searchError}</p>
                  <Button onClick={handleReset} variant="warm" className="mt-5">
                    重新开始
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

      {!isInFlow && <Footer />}
    </div>
  );
}
