import type {
  AgentExecutionStep,
  AuthPayload,
  GeoLocation,
  HighSchoolRecommendation,
  HighSchoolRecommendRequest,
  MessageReply,
  MessageThread,
  School,
  SchoolAnalysis,
  SchoolRanking,
  SearchRequest,
  SearchResponse,
  SessionUser,
  SiteMessageReply,
  SiteMessageThread,
  UniversityAnalysis,
  UniversityRanking,
  UniversitySearchRequest,
} from "@/types/school";

const API_BASE = "/api";

let authClearCallback: (() => void) | null = null;

export function setAuthClearCallback(callback: () => void) {
  authClearCallback = callback;
}

function authHeaders(token?: string): HeadersInit {
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function readApiResponse<T>(res: Response, fallback: string): Promise<T> {
  if (res.status === 401) {
    authClearCallback?.();
    throw new Error("登录已过期，请重新登录");
  }
  const json = await res.json();
  if (!res.ok || !json.success) {
    throw new Error(json.message || fallback);
  }
  return json.data as T;
}

export async function registerWithEmail(request: {
  email: string;
  password: string;
  displayName: string;
  verificationCode: string;
}): Promise<AuthPayload> {
  const res = await fetch(`${API_BASE}/auth/register`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  return readApiResponse<AuthPayload>(res, "注册失败");
}

export async function sendRegisterCode(email: string): Promise<void> {
  const res = await fetch(`${API_BASE}/auth/register/code`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email }),
  });
  await readApiResponse<object>(res, "验证码发送失败");
}

export async function loginWithEmail(request: {
  email: string;
  password: string;
}): Promise<AuthPayload> {
  const res = await fetch(`${API_BASE}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  return readApiResponse<AuthPayload>(res, "登录失败");
}

export async function getCurrentUser(
  token: string,
): Promise<SessionUser | null> {
  const res = await fetch(`${API_BASE}/auth/me`, {
    headers: { ...authHeaders(token) },
  });
  if (!res.ok) return null;
  const json = await res.json();
  return json.success ? (json.data as SessionUser) : null;
}

export async function fetchMessages(
  schoolId?: string,
): Promise<MessageThread[]> {
  const params = new URLSearchParams();
  if (schoolId) params.set("schoolId", schoolId);
  const query = params.toString();
  const res = await fetch(`${API_BASE}/messages${query ? `?${query}` : ""}`);
  return readApiResponse<MessageThread[]>(res, "获取留言失败");
}

export async function createMessage(
  token: string,
  request: { schoolId: string; schoolName: string; content: string },
): Promise<MessageThread> {
  const res = await fetch(`${API_BASE}/messages`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify(request),
  });
  return readApiResponse<MessageThread>(res, "发布留言失败");
}

export async function createMessageReply(
  token: string,
  messageId: number,
  content: string,
): Promise<MessageReply> {
  const res = await fetch(`${API_BASE}/messages/${messageId}/replies`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify({ content }),
  });
  return readApiResponse<MessageReply>(res, "回复留言失败");
}

export async function fetchSiteMessages(): Promise<SiteMessageThread[]> {
  const res = await fetch(`${API_BASE}/site-messages`);
  return readApiResponse<SiteMessageThread[]>(res, "获取留言失败");
}

export async function createSiteMessage(
  token: string,
  content: string,
): Promise<SiteMessageThread> {
  const res = await fetch(`${API_BASE}/site-messages`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify({ content }),
  });
  return readApiResponse<SiteMessageThread>(res, "发布留言失败");
}

export async function createSiteMessageReply(
  token: string,
  messageId: number,
  content: string,
): Promise<SiteMessageReply> {
  const res = await fetch(`${API_BASE}/site-messages/${messageId}/replies`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify({ content }),
  });
  return readApiResponse<SiteMessageReply>(res, "回复留言失败");
}

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

export async function trackSiteVisit(path: string): Promise<void> {
  const params = new URLSearchParams({ path });
  await fetch(`${API_BASE}/analytics/visit?${params.toString()}`, {
    method: "POST",
  });
}

export type SearchStreamEvent =
  | {
      type: "meta";
      location: { address: string; latitude: number; longitude: number };
      schools: School[];
      searchRadius: number;
      totalFound: number;
    }
  | {
      type: "analysis";
      schoolId: string;
      analysis: SchoolAnalysis;
      overallScore: number;
      admissionMatchScore?: number;
    }
  | { type: "agent_step"; step: AgentExecutionStep }
  | { type: "done"; rankings: SchoolRanking[]; message?: string }
  | { type: "error"; message: string };

function createSseFrameParser<TEvent>() {
  return (frame: string): TEvent | null => {
    let eventName = "message";
    const dataLines: string[] = [];
    for (const rawLine of frame.split("\n")) {
      const line = rawLine.replace(/\r$/, "");
      if (!line || line.startsWith(":")) continue;
      const idx = line.indexOf(":");
      const field = idx === -1 ? line : line.slice(0, idx);
      let value = idx === -1 ? "" : line.slice(idx + 1);
      if (value.startsWith(" ")) value = value.slice(1);
      if (field === "event") eventName = value;
      else if (field === "data") dataLines.push(value);
    }
    if (dataLines.length === 0) return null;
    try {
      const data = JSON.parse(dataLines.join("\n"));
      return { type: eventName, ...data } as TEvent;
    } catch (e) {
      console.warn("[SSE] 跳过无法解析的帧:", e, dataLines.join("\n"));
      return null;
    }
  };
}

async function* readSseStream<TEvent>(
  res: Response,
): AsyncGenerator<TEvent, void, unknown> {
  if (!res.body) {
    throw new Error("浏览器不支持流式响应");
  }

  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  const parseFrame = createSseFrameParser<TEvent>();

  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });

      let sepIndex: number;
      while (
        (sepIndex = (() => {
          const a = buffer.indexOf("\n\n");
          const b = buffer.indexOf("\r\n\r\n");
          if (a === -1) return b;
          if (b === -1) return a;
          return Math.min(a, b);
        })()) >= 0
      ) {
        const sepLen = buffer.startsWith("\r\n\r\n", sepIndex) ? 4 : 2;
        const frame = buffer.slice(0, sepIndex);
        buffer = buffer.slice(sepIndex + sepLen);
        const event = parseFrame(frame);
        if (event) yield event;
      }
    }

    buffer += decoder.decode();
    const tail = buffer.trim();
    if (tail) {
      const event = parseFrame(tail);
      if (event) yield event;
    }
  } finally {
    try {
      reader.releaseLock();
    } catch {
      // ignore
    }
  }
}

export async function* searchSchoolsStream(
  request: SearchRequest,
  signal?: AbortSignal,
  token?: string,
): AsyncGenerator<SearchStreamEvent, void, unknown> {
  const res = await fetch(`${API_BASE}/schools/search`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Accept: "text/event-stream",
      ...authHeaders(token),
    },
    body: JSON.stringify(request),
    signal,
  });

  if (!res.ok) {
    let message = `搜索失败: ${res.statusText}`;
    try {
      const json = await res.json();
      if (json?.message) message = json.message;
    } catch {
      // ignore parse error
    }
    throw new Error(message);
  }

  for await (const event of readSseStream<SearchStreamEvent>(res)) {
    yield event;
  }
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

export interface NearbyFacility {
  id: string;
  name: string;
  address: string;
  distance: string;
  location: string;
}

export async function fetchNearbyFacilities(
  latitude: number,
  longitude: number,
  type: string,
  radius?: number,
): Promise<NearbyFacility[]> {
  const params = new URLSearchParams({
    latitude: String(latitude),
    longitude: String(longitude),
    type,
  });
  if (radius) params.set("radius", String(radius));
  const res = await fetch(`${API_BASE}/schools/nearby?${params}`);
  if (!res.ok) {
    throw new Error(`周边设施查询失败: ${res.statusText}`);
  }
  const json = await res.json();
  if (!json.success) {
    throw new Error(json.message || "周边设施查询失败");
  }
  return json.data;
}

// ═══════════════════════════════════════════════════════════
// 大学报考搜索
// ═══════════════════════════════════════════════════════════

export type UniversitySearchStreamEvent =
  | {
      type: "meta";
      studentScore: number;
      universityNames: string[];
      preferredMajors?: string[];
      totalCount: number;
    }
  | {
      type: "analysis";
      universityName: string;
      analysis: UniversityAnalysis;
    }
  | { type: "agent_step"; step: AgentExecutionStep }
  | { type: "done"; rankings: UniversityRanking[]; message?: string }
  | { type: "error"; message: string };

export async function* searchUniversitiesStream(
  request: UniversitySearchRequest,
  signal?: AbortSignal,
  token?: string,
): AsyncGenerator<UniversitySearchStreamEvent, void, unknown> {
  const res = await fetch(`${API_BASE}/universities/search`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Accept: "text/event-stream",
      ...authHeaders(token),
    },
    body: JSON.stringify(request),
    signal,
  });

  if (!res.ok) {
    let message = `搜索失败: ${res.statusText}`;
    try {
      const json = await res.json();
      if (json?.message) message = json.message;
    } catch {
      // ignore parse error
    }
    throw new Error(message);
  }

  for await (const event of readSseStream<UniversitySearchStreamEvent>(res)) {
    yield event;
  }
}

// ═══════════════════════════════════════════════════════════
// 高中择校推荐（按省份 + 分数，冲/稳/保）
// ═══════════════════════════════════════════════════════════

export type HighSchoolRecommendStreamEvent =
  | {
      type: "meta";
      studentScore: number;
      province: string;
    }
  | { type: "agent_step"; step: AgentExecutionStep }
  | {
      type: "done";
      recommendations: HighSchoolRecommendation[];
      message?: string;
    }
  | { type: "error"; message: string };

export async function* recommendHighSchoolsStream(
  request: HighSchoolRecommendRequest,
  signal?: AbortSignal,
  token?: string,
): AsyncGenerator<HighSchoolRecommendStreamEvent, void, unknown> {
  const res = await fetch(`${API_BASE}/highschools/recommend`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Accept: "text/event-stream",
      ...authHeaders(token),
    },
    body: JSON.stringify(request),
    signal,
  });

  if (!res.ok) {
    let message = `推荐失败: ${res.statusText}`;
    try {
      const json = await res.json();
      if (json?.message) message = json.message;
    } catch {
      // ignore parse error
    }
    throw new Error(message);
  }

  for await (const event of readSseStream<HighSchoolRecommendStreamEvent>(
    res,
  )) {
    yield event;
  }
}

// ═══════════════════════════════════════════════════════════
// 历史分析记录
// ═══════════════════════════════════════════════════════════

export type HistorySessionType = "school" | "university" | "highschool";

export interface AnalysisHistoryItem {
  id: number;
  sessionType: HistorySessionType;
  title: string;
  summary?: string;
  createdAt: string; // ISO 8601
}

export interface AnalysisHistoryDetail extends AnalysisHistoryItem {
  requestJson: string;
  resultJson: string;
}

export interface AnalysisHistoryListResult {
  items: AnalysisHistoryItem[];
  total: number;
  page: number;
  pageSize: number;
}

export async function getAnalysisHistory(
  token: string,
  page = 1,
  pageSize = 20,
): Promise<AnalysisHistoryListResult> {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });
  const res = await fetch(`${API_BASE}/history?${params}`, {
    headers: { ...authHeaders(token) },
  });
  return readApiResponse<AnalysisHistoryListResult>(res, "获取历史记录失败");
}

export async function getAnalysisHistoryDetail(
  id: number,
  token: string,
): Promise<AnalysisHistoryDetail> {
  const res = await fetch(`${API_BASE}/history/${id}`, {
    headers: { ...authHeaders(token) },
  });
  return readApiResponse<AnalysisHistoryDetail>(res, "获取历史记录详情失败");
}

export async function deleteAnalysisHistory(
  id: number,
  token: string,
): Promise<void> {
  const res = await fetch(`${API_BASE}/history/${id}`, {
    method: "DELETE",
    headers: { ...authHeaders(token) },
  });
  await readApiResponse<unknown>(res, "删除历史记录失败");
}

export async function deleteAllAnalysisHistory(token: string): Promise<void> {
  const res = await fetch(`${API_BASE}/history`, {
    method: "DELETE",
    headers: { ...authHeaders(token) },
  });
  await readApiResponse<unknown>(res, "清空历史记录失败");
}
