import { useState, useCallback } from "react";
import type { GeoLocation, GeoLocationStatus } from "@/types/school";
import { getLocationByIp, reverseGeocodeCoords } from "@/lib/api";

function isGeolocationPositionError(
  error: unknown,
): error is GeolocationPositionError {
  return (
    typeof error === "object" &&
    error !== null &&
    "code" in error &&
    typeof (error as { code: unknown }).code === "number"
  );
}

function getBrowserPosition(): Promise<GeolocationPosition> {
  return new Promise((resolve, reject) => {
    if (typeof navigator === "undefined" || !("geolocation" in navigator)) {
      reject(new Error("当前浏览器不支持定位，请手动输入地址"));
      return;
    }

    navigator.geolocation.getCurrentPosition(resolve, reject, {
      enableHighAccuracy: true,
      timeout: 10000,
      maximumAge: 0,
    });
  });
}

function getBrowserLocationError(error: GeolocationPositionError): string {
  switch (error.code) {
    case 1: // PERMISSION_DENIED
      return "IP定位失败，且浏览器定位权限被拒绝，请手动输入地址";
    case 2: // POSITION_UNAVAILABLE
      return "IP定位失败，且浏览器暂时无法获取位置信息，请手动输入地址";
    case 3: // TIMEOUT
      return "IP定位失败，且浏览器定位超时，请手动输入地址";
    default:
      return "定位失败，请手动输入地址";
  }
}

export function useGeolocation() {
  const [location, setLocation] = useState<GeoLocation | null>(null);
  const [status, setStatus] = useState<GeoLocationStatus>("idle");
  const [error, setError] = useState<string | null>(null);

  const requestLocation = useCallback(async () => {
    setStatus("requesting");
    setError(null);

    const browserSupported =
      typeof navigator !== "undefined" && "geolocation" in navigator;

    // 1) 优先使用浏览器 Geolocation API 获取经纬度
    let browserErrorMessage: string | null = null;
    let browserPermissionDenied = false;
    if (browserSupported) {
      try {
        const position = await getBrowserPosition();
        const { latitude, longitude, accuracy } = position.coords;

        let address = "当前位置";
        try {
          address = await reverseGeocodeCoords(latitude, longitude);
        } catch (reverseError) {
          console.warn(
            "[useGeolocation] 逆地理编码失败，使用默认地址标签:",
            reverseError,
          );
        }

        setLocation({ latitude, longitude, address, accuracy });
        setStatus("success");
        return;
      } catch (browserError) {
        console.warn(
          "[useGeolocation] 浏览器定位失败，回退到 IP 定位:",
          browserError,
        );
        if (isGeolocationPositionError(browserError)) {
          browserPermissionDenied = browserError.code === 1;
          browserErrorMessage = getBrowserLocationError(browserError);
        } else {
          browserErrorMessage =
            browserError instanceof Error
              ? browserError.message
              : "浏览器定位失败";
        }
      }
    } else {
      console.warn("[useGeolocation] 当前浏览器不支持定位，回退到 IP 定位");
      browserErrorMessage = "当前浏览器不支持定位";
    }

    // 2) 浏览器定位失败或不支持 → 调用后端 IP 定位接口
    try {
      const geo = await getLocationByIp();
      if (
        Number.isFinite(geo.latitude) &&
        Number.isFinite(geo.longitude) &&
        geo.latitude !== 0 &&
        geo.longitude !== 0
      ) {
        setLocation(geo);
        setStatus("success");
        return;
      }
      console.warn("[useGeolocation] IP 定位返回坐标无效:", geo);
      setStatus(browserPermissionDenied ? "denied" : "error");
      setError(
        `${browserErrorMessage ?? "浏览器定位失败"}；IP定位返回坐标无效，请手动输入地址`,
      );
    } catch (ipError) {
      console.warn("[useGeolocation] IP 定位接口失败:", ipError);
      const ipMessage =
        ipError instanceof Error ? ipError.message : "IP定位失败";
      setStatus(browserPermissionDenied ? "denied" : "error");
      setError(
        `${browserErrorMessage ?? "浏览器定位失败"}；IP定位也失败：${ipMessage}`,
      );
    }
  }, []);

  const setManualLocation = useCallback((geo: GeoLocation) => {
    setLocation(geo);
    setStatus("success");
    setError(null);
  }, []);

  return {
    location,
    status,
    error,
    requestLocation,
    setManualLocation,
  };
}
