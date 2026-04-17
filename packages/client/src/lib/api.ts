import type {
  GeoLocation,
  SchoolRanking,
  SearchRequest,
  SearchResponse,
} from "@/types/school";

const API_BASE = "/api";

export async function searchSchools(
  request: SearchRequest,
): Promise<SearchResponse> {
  const res = await fetch(`${API_BASE}/schools/search`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  if (!res.ok) {
    throw new Error(`搜索失败: ${res.statusText}`);
  }
  return res.json();
}

export async function getSchoolDetail(id: string): Promise<SchoolRanking> {
  const res = await fetch(`${API_BASE}/schools/${encodeURIComponent(id)}`);
  if (!res.ok) {
    throw new Error(`获取学校详情失败: ${res.statusText}`);
  }
  return res.json();
}

export async function geocodeAddress(
  address: string,
): Promise<{ latitude: number; longitude: number; address: string }> {
  const res = await fetch(`${API_BASE}/location/geocode`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ address }),
  });
  if (!res.ok) {
    throw new Error(`地址解析失败: ${res.statusText}`);
  }
  const json = await res.json();
  if (!json.success) {
    throw new Error(json.message || "地址解析失败");
  }
  return json.data;
}

export async function getLocationByIp(): Promise<GeoLocation> {
  const res = await fetch(`${API_BASE}/location/ip`);
  if (!res.ok) {
    throw new Error(`IP定位失败: ${res.statusText}`);
  }
  const json = await res.json();
  if (!json.success) {
    throw new Error(json.message || "IP定位失败");
  }

  const data = json.data as {
    latitude: number;
    longitude: number;
    province?: string;
    city?: string;
    district?: string;
    address?: string;
  };

  const address =
    data.address ||
    [data.province, data.city, data.district].filter(Boolean).join("") ||
    "当前位置";

  return {
    latitude: data.latitude,
    longitude: data.longitude,
    address,
  };
}

export async function reverseGeocodeCoords(
  latitude: number,
  longitude: number,
): Promise<string> {
  const res = await fetch(`${API_BASE}/location/reverse`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ latitude, longitude }),
  });
  if (!res.ok) {
    throw new Error(`逆地理编码失败: ${res.statusText}`);
  }
  const json = await res.json();
  if (!json.success) {
    throw new Error(json.message || "逆地理编码失败");
  }
  return json.data.address as string;
}
