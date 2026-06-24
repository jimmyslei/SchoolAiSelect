import { useMemo, useState } from "react";
import { AnimatePresence, motion } from "framer-motion";
import {
  BookOpen,
  ChevronLeft,
  Clock3,
  GraduationCap,
  Loader2,
  School,
  Sparkles,
  Trash2,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { SchoolRankingList } from "@/components/results/SchoolRanking";
import { UniversityRankingList } from "@/components/results/UniversityRanking";
import { HighSchoolRecommendRanking } from "@/components/results/HighSchoolRecommendRanking";
import type {
  AnalysisHistoryDetail,
  AnalysisHistoryItem,
  HistorySessionType,
} from "@/lib/api";
import { useAnalysisHistory } from "@/hooks/useAnalysisHistory";
import type {
  HighSchoolRecommendation,
  SchoolRanking,
  SchoolType,
  UniversityAnalysis,
  UniversityRanking,
} from "@/types/school";

interface AnalysisHistoryPanelProps {
  token: string | null;
  onRequireAuth: () => void;
}

type HistoryDetailState =
  | {
      sessionType: "school";
      payload: SchoolHistoryPayload;
      raw: AnalysisHistoryDetail;
    }
  | {
      sessionType: "university";
      payload: UniversityHistoryPayload;
      raw: AnalysisHistoryDetail;
    }
  | {
      sessionType: "highschool";
      payload: HighSchoolHistoryPayload;
      raw: AnalysisHistoryDetail;
    };

interface SchoolHistoryPayload {
  schoolType: SchoolType;
  locationAddress: string;
  rankings: SchoolRanking[];
  analyzedCount?: number;
  totalCount?: number;
}

interface UniversityHistoryPayload {
  studentScore: number;
  universityNames?: string[];
  rankings: UniversityRanking[];
  analyzedCount?: number;
  totalCount?: number;
}

interface HighSchoolHistoryPayload {
  studentScore: number;
  province: string;
  recommendations: HighSchoolRecommendation[];
}

const HISTORY_META: Record<
  HistorySessionType,
  {
    label: string;
    icon: typeof BookOpen;
    badgeClass: string;
  }
> = {
  school: {
    label: "附近择校",
    icon: BookOpen,
    badgeClass: "bg-warm-100 text-warm-700",
  },
  university: {
    label: "大学报考",
    icon: GraduationCap,
    badgeClass: "bg-purple-100 text-purple-700",
  },
  highschool: {
    label: "高中推荐",
    icon: School,
    badgeClass: "bg-green-100 text-green-700",
  },
};

function formatTime(ts: string) {
  const d = new Date(ts.substring(0, 19));
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffMin = Math.floor(diffMs / 60_000);
  if (diffMin < 1) return "刚刚";
  if (diffMin < 60) return `${diffMin} 分钟前`;
  const diffH = Math.floor(diffMin / 60);
  if (diffH < 24) return `${diffH} 小时前`;
  const diffD = Math.floor(diffH / 24);
  if (diffD < 7) return `${diffD} 天前`;
  return d.toLocaleDateString("zh-CN", { month: "short", day: "numeric" });
}

function parseHistoryDetail(detail: AnalysisHistoryDetail): HistoryDetailState {
  const payload = JSON.parse(detail.resultJson) as Record<string, unknown>;

  if (detail.sessionType === "school") {
    return {
      sessionType: "school",
      raw: detail,
      payload: {
        schoolType: payload.schoolType as SchoolType,
        locationAddress: String(payload.locationAddress ?? "历史分析结果"),
        rankings: (payload.rankings as SchoolRanking[]) ?? [],
        analyzedCount: Number(payload.analyzedCount ?? 0),
        totalCount: Number(payload.totalCount ?? 0),
      },
    };
  }

  if (detail.sessionType === "university") {
    return {
      sessionType: "university",
      raw: detail,
      payload: {
        studentScore: Number(payload.studentScore ?? 0),
        universityNames: (payload.universityNames as string[]) ?? [],
        rankings: (payload.rankings as UniversityRanking[]) ?? [],
        analyzedCount: Number(payload.analyzedCount ?? 0),
        totalCount: Number(payload.totalCount ?? 0),
      },
    };
  }

  return {
    sessionType: "highschool",
    raw: detail,
    payload: {
      studentScore: Number(payload.studentScore ?? 0),
      province: String(payload.province ?? "未知省份"),
      recommendations:
        (payload.recommendations as HighSchoolRecommendation[]) ?? [],
    },
  };
}

export function AnalysisHistoryPanel({
  token,
  onRequireAuth,
}: AnalysisHistoryPanelProps) {
  const {
    items,
    total,
    loading,
    error,
    getDetail,
    deleteItem,
    deleteAll,
    refresh,
  } = useAnalysisHistory({ token: token ?? undefined, pageSize: 20 });

  const [detail, setDetail] = useState<HistoryDetailState | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const hasItems = items.length > 0;
  const emptyText = useMemo(() => {
    if (!token) return "登录后可查看跨设备保存的历史分析结果";
    return "还没有历史分析记录，完成一次分析后会自动保存到这里";
  }, [token]);

  const handleOpen = async (item: AnalysisHistoryItem) => {
    if (!token) {
      onRequireAuth();
      return;
    }
    setActionError(null);
    setDetailLoading(true);
    try {
      const result = await getDetail(item.id);
      setDetail(parseHistoryDetail(result));
    } catch (err) {
      setActionError(err instanceof Error ? err.message : "加载详情失败");
    } finally {
      setDetailLoading(false);
    }
  };

  const handleDelete = async (id: number) => {
    if (!token) {
      onRequireAuth();
      return;
    }
    setActionError(null);
    try {
      await deleteItem(id);
      if (detail?.raw.id === id) {
        setDetail(null);
      }
    } catch (err) {
      setActionError(err instanceof Error ? err.message : "删除失败");
    }
  };

  const handleDeleteAll = async () => {
    if (!token) {
      onRequireAuth();
      return;
    }
    setActionError(null);
    try {
      await deleteAll();
      setDetail(null);
    } catch (err) {
      setActionError(err instanceof Error ? err.message : "清空失败");
    }
  };

  return (
    <section className="mx-auto max-w-6xl px-4 py-6 sm:px-6">
      <div className="overflow-hidden rounded-4xl border border-border/40 bg-linear-to-br from-white to-warm-50/30 p-5 shadow-sm backdrop-blur-sm sm:p-6">
        <div className="flex flex-col gap-3 border-b border-border/40 pb-5 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full bg-background/70 px-3 py-1 text-xs font-medium text-warm-700">
              <Sparkles className="h-3.5 w-3.5" />
              云端历史分析
            </div>
            <h3 className="mt-3 text-xl font-semibold text-neutral-900 sm:text-2xl">
              我的历史分析记录
            </h3>
            <p className="mt-2 text-sm leading-6 text-neutral-500">
              服务器会自动保存最近的分析结果，支持直接回看学校择校、大学报考与高中推荐。
            </p>
          </div>

          <div className="flex items-center gap-2">
            <span className="rounded-full bg-background/80 px-3 py-1 text-xs text-neutral-500 shadow-xs">
              共 {total} 条
            </span>
            {hasItems && (
              <Button
                variant="ghost"
                size="sm"
                onClick={handleDeleteAll}
                className="text-neutral-500 hover:bg-warm-100 hover:text-warm-700"
              >
                <Trash2 className="mr-1.5 h-3.5 w-3.5" />
                清空全部
              </Button>
            )}
          </div>
        </div>

        {detail ? (
          <div className="mt-6 max-h-[calc(100vh-4rem)] space-y-5 overflow-y-auto pr-1 sm:max-h-[calc(100vh-4rem)]">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="text-sm font-medium text-neutral-500">
                  {HISTORY_META[detail.sessionType].label}
                </p>
                <h4 className="mt-1 text-2xl font-semibold text-neutral-900">
                  {detail.raw.title}
                </h4>
                {detail.raw.summary && (
                  <p className="mt-2 text-sm text-neutral-500">
                    {detail.raw.summary}
                  </p>
                )}
              </div>
              <Button variant="outline" onClick={() => setDetail(null)}>
                <ChevronLeft className="mr-1.5 h-4 w-4" />
                返回历史
              </Button>
            </div>

            {detail.sessionType === "school" && (
              <SchoolRankingList
                rankings={detail.payload.rankings}
                schoolType={detail.payload.schoolType}
                locationAddress={detail.payload.locationAddress}
                analyzedCount={detail.payload.analyzedCount}
                totalCount={detail.payload.totalCount}
                onReset={() => setDetail(null)}
              />
            )}

            {detail.sessionType === "university" && (
              <UniversityRankingList
                rankings={detail.payload.rankings}
                analysesMap={
                  new Map<string, UniversityAnalysis>(
                    detail.payload.rankings.map((ranking) => [
                      ranking.analysis.info.name,
                      ranking.analysis,
                    ]),
                  )
                }
                studentScore={detail.payload.studentScore}
                analyzedCount={
                  detail.payload.analyzedCount ?? detail.payload.rankings.length
                }
                totalCount={
                  detail.payload.totalCount ?? detail.payload.rankings.length
                }
                onReset={() => setDetail(null)}
              />
            )}

            {detail.sessionType === "highschool" && (
              <HighSchoolRecommendRanking
                recommendations={detail.payload.recommendations}
                studentScore={detail.payload.studentScore}
                province={detail.payload.province}
                onReset={() => setDetail(null)}
              />
            )}
          </div>
        ) : (
          <div className="mt-6 grid gap-6 lg:max-h-[calc(100vh-24rem)] lg:grid-cols-[360px_minmax(0,1fr)] lg:items-start sm:mt-6">
            <div className="space-y-3 lg:max-h-[calc(100vh-24rem)] lg:overflow-y-auto lg:pr-1">
              {loading ? (
                <div className="flex items-center gap-2 rounded-3xl bg-background/70 px-4 py-8 text-sm text-neutral-500">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  正在加载历史记录...
                </div>
              ) : hasItems ? (
                <AnimatePresence mode="popLayout">
                  {items.map((item) => {
                    const meta = HISTORY_META[item.sessionType];
                    const Icon = meta.icon;
                    return (
                      <motion.button
                        key={item.id}
                        layout
                        initial={{ opacity: 0, y: 8 }}
                        animate={{ opacity: 1, y: 0 }}
                        exit={{ opacity: 0, scale: 0.96 }}
                        type="button"
                        onClick={() => void handleOpen(item)}
                        className="group w-full rounded-3xl border border-border/40 bg-background/70 p-4 text-left transition-all hover:-translate-y-0.5 hover:border-warm-200 hover:bg-warm-50/80"
                      >
                        <div className="flex items-start gap-3">
                          <div
                            className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl ${meta.badgeClass}`}
                          >
                            <Icon className="h-4 w-4" />
                          </div>
                          <div className="min-w-0 flex-1">
                            <div className="flex flex-wrap items-center gap-2">
                              <span className="rounded-full bg-white/80 px-2.5 py-0.5 text-[11px] font-medium text-neutral-500 shadow-xs">
                                {meta.label}
                              </span>
                              <span className="inline-flex items-center gap-1 text-xs text-neutral-400">
                                <Clock3 className="h-3 w-3" />
                                {formatTime(item.createdAt)}
                              </span>
                            </div>
                            <h4 className="mt-2 line-clamp-2 text-sm font-semibold text-neutral-800">
                              {item.title}
                            </h4>
                            {item.summary && (
                              <p className="mt-1 line-clamp-2 text-xs leading-5 text-neutral-500">
                                {item.summary}
                              </p>
                            )}
                          </div>
                          <button
                            type="button"
                            onClick={(e) => {
                              e.stopPropagation();
                              void handleDelete(item.id);
                            }}
                            className="rounded-full p-2 text-neutral-300 opacity-0 transition-all hover:bg-peach-50 hover:text-peach-500 group-hover:opacity-100"
                            aria-label="删除历史记录"
                          >
                            <Trash2 className="h-4 w-4" />
                          </button>
                        </div>
                      </motion.button>
                    );
                  })}
                </AnimatePresence>
              ) : (
                <div className="rounded-3xl border border-dashed border-border/50 bg-background/50 px-5 py-10 text-center">
                  <p className="text-sm leading-6 text-neutral-500">
                    {emptyText}
                  </p>
                  {!token && (
                    <Button
                      variant="warm"
                      size="sm"
                      onClick={onRequireAuth}
                      className="mt-4"
                    >
                      立即登录
                    </Button>
                  )}
                </div>
              )}
            </div>

            <div className="rounded-4xl border border-border/40 bg-background/60 p-6 lg:max-h-[calc(100vh-14rem)] lg:overflow-y-auto">
              {detailLoading ? (
                <div className="flex min-h-60 items-center justify-center gap-3 text-neutral-500">
                  <Loader2 className="h-5 w-5 animate-spin" />
                  正在加载分析详情...
                </div>
              ) : (
                <div className="flex min-h-60 flex-col justify-center text-center">
                  <p className="text-lg font-medium text-neutral-800">
                    选择一条历史记录查看完整分析结果
                  </p>
                  <p className="mt-3 text-sm leading-6 text-neutral-500">
                    打开后会直接展示当时的学校排名、大学报考建议或高中推荐内容，无需再次发起分析。
                  </p>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => void refresh()}
                    className="mx-auto mt-5"
                  >
                    刷新记录
                  </Button>
                </div>
              )}
            </div>
          </div>
        )}

        {(error || actionError) && (
          <div className="mt-4 rounded-2xl bg-peach-50 px-4 py-3 text-sm text-peach-700">
            {actionError ?? error}
          </div>
        )}
      </div>
    </section>
  );
}
