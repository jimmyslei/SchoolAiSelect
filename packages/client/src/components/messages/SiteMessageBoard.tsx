import { useEffect, useRef, useState } from "react";
import { MessageSquare, Send, X, UserRound, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  createSiteMessage,
  createSiteMessageReply,
  fetchSiteMessages,
} from "@/lib/api";
import type { SessionUser, SiteMessageThread } from "@/types/school";

interface SiteMessageBoardProps {
  token: string | null;
  user: SessionUser | null;
  onRequireAuth: () => void;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("zh-CN", {
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(value));
}

export function SiteMessageBoard({
  token,
  user,
  onRequireAuth,
}: SiteMessageBoardProps) {
  const [open, setOpen] = useState(false);
  const [messages, setMessages] = useState<SiteMessageThread[]>([]);
  const [content, setContent] = useState("");
  const [replyDrafts, setReplyDrafts] = useState<Record<number, string>>({});
  const [openReplyId, setOpenReplyId] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const loadedRef = useRef(false);
  const scrollRef = useRef<HTMLDivElement>(null);

  // 打开时加载留言（只加载一次）
  useEffect(() => {
    if (!open || loadedRef.current) return;
    loadedRef.current = true;
    setLoading(true);
    fetchSiteMessages()
      .then((items) => setMessages(items))
      .catch((err) =>
        setError(err instanceof Error ? err.message : "留言加载失败"),
      )
      .finally(() => setLoading(false));
  }, [open]);

  // ESC 关闭
  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [open]);

  const handleSubmit = async () => {
    if (!token || !user) {
      onRequireAuth();
      return;
    }
    if (!content.trim()) return;
    setSubmitting(true);
    setError(null);
    try {
      const created = await createSiteMessage(token, content.trim());
      setMessages((prev) => [created, ...prev]);
      setContent("");
      // 滚动到顶部看新留言
      scrollRef.current?.scrollTo({ top: 0, behavior: "smooth" });
    } catch (err) {
      setError(err instanceof Error ? err.message : "发布失败");
    } finally {
      setSubmitting(false);
    }
  };

  const handleReply = async (messageId: number) => {
    if (!token || !user) {
      onRequireAuth();
      return;
    }
    const replyContent = replyDrafts[messageId]?.trim();
    if (!replyContent) return;
    setError(null);
    try {
      const reply = await createSiteMessageReply(
        token,
        messageId,
        replyContent,
      );
      setMessages((prev) =>
        prev.map((m) =>
          m.id === messageId ? { ...m, replies: [...m.replies, reply] } : m,
        ),
      );
      setReplyDrafts((d) => ({ ...d, [messageId]: "" }));
      setOpenReplyId(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "回复失败");
    }
  };

  return (
    <>
      {/* ─── 悬浮按钮 ─── */}
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-label="打开留言板"
        className="fixed bottom-6 right-6 z-50 flex h-14 w-14 items-center justify-center rounded-full bg-warm-500 text-white shadow-lg shadow-warm-500/25 transition-all hover:scale-110 hover:bg-warm-600 hover:shadow-xl hover:shadow-warm-500/30 active:scale-95 sm:bottom-8 sm:right-8"
      >
        <MessageSquare className="h-6 w-6" />
      </button>

      {/* ─── 背景遮罩 + 右侧滑出面板 ─── */}
      {open && (
        <div className="fixed inset-0 z-[100] flex justify-end">
          {/* 遮罩 */}
          <div
            className="absolute inset-0 bg-black/30 backdrop-blur-sm"
            onClick={() => setOpen(false)}
          />
          {/* 面板 */}
          <div className="relative z-10 flex h-full w-full max-w-md flex-col bg-gradient-to-b from-white to-warm-50/30 shadow-2xl animate-slide-in-right">
            {/* ── 顶栏 ── */}
            <div className="flex items-center justify-between border-b border-border/60 px-5 py-4">
              <div className="flex items-center gap-2.5">
                <div className="flex h-9 w-9 items-center justify-center rounded-full bg-warm-100 text-warm-600">
                  <MessageSquare className="h-4.5 w-4.5" />
                </div>
                <div>
                  <h2 className="font-display text-lg font-semibold text-neutral-900">
                    系统留言板
                  </h2>
                  <p className="text-xs text-neutral-400">欢迎反馈意见与建议</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setOpen(false)}
                aria-label="关闭"
                className="flex h-8 w-8 items-center justify-center rounded-full text-neutral-400 transition hover:bg-neutral-100 hover:text-neutral-600"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            {/* ── 消息列表（可滚动） ── */}
            <div
              ref={scrollRef}
              className="flex-1 space-y-3 overflow-y-auto px-4 py-4"
            >
              {loading && (
                <p className="py-8 text-center text-sm text-neutral-400">
                  加载中...
                </p>
              )}
              {!loading && messages.length === 0 && (
                <div className="flex flex-col items-center py-16 text-center">
                  <MessageSquare className="mb-3 h-10 w-10 text-neutral-200" />
                  <p className="text-sm text-neutral-400">暂无留言</p>
                  <p className="mt-1 text-xs text-neutral-300">
                    欢迎成为第一个留言的用户
                  </p>
                </div>
              )}

              {messages.map((message) => (
                <article
                  key={message.id}
                  className="rounded-2xl border border-border/50 bg-white/80 p-3.5"
                >
                  <div className="flex items-start gap-2.5">
                    <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-sky-100 text-sky-600">
                      <UserRound className="h-3.5 w-3.5" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                        <span className="text-sm font-medium text-neutral-900">
                          {message.authorName}
                        </span>
                        <span className="text-[11px] text-neutral-400">
                          {formatDate(message.createdAt)}
                        </span>
                      </div>
                      <p className="mt-1.5 whitespace-pre-wrap text-sm leading-6 text-neutral-700">
                        {message.content}
                      </p>

                      {/* 管理员回复列表 */}
                      {message.replies.length > 0 && (
                        <div className="mt-2.5 space-y-1.5">
                          {message.replies.map((reply) => (
                            <div
                              key={reply.id}
                              className={`rounded-xl px-3 py-2 text-[13px] ${
                                reply.isAdmin
                                  ? "bg-warm-50 border border-warm-200/60"
                                  : "bg-sand-50"
                              }`}
                            >
                              <div className="flex items-center gap-1.5">
                                <span className="font-medium text-neutral-800">
                                  {reply.authorName}
                                </span>
                                {reply.isAdmin && (
                                  <span className="flex items-center gap-0.5 rounded-full bg-warm-100 px-1.5 py-0.5 text-[10px] font-medium text-warm-700">
                                    <ShieldCheck className="h-2.5 w-2.5" />
                                    管理员
                                  </span>
                                )}
                                <span className="ml-auto text-[10px] text-neutral-400">
                                  {formatDate(reply.createdAt)}
                                </span>
                              </div>
                              <p className="mt-1 whitespace-pre-wrap leading-5 text-neutral-600">
                                {reply.content}
                              </p>
                            </div>
                          ))}
                        </div>
                      )}

                      {/* 管理员回复入口 */}
                      {user?.isAdmin && (
                        <div className="mt-2">
                          {openReplyId === message.id ? (
                            <div className="flex gap-1.5">
                              <input
                                autoFocus
                                value={replyDrafts[message.id] ?? ""}
                                onChange={(e) =>
                                  setReplyDrafts((d) => ({
                                    ...d,
                                    [message.id]: e.target.value,
                                  }))
                                }
                                placeholder="以管理员身份回复..."
                                className="h-8 min-w-0 flex-1 rounded-full border border-border/80 bg-background/80 px-3 text-xs outline-none transition focus:border-warm-300 focus:ring-2 focus:ring-warm-100"
                                maxLength={1000}
                                onKeyDown={(e) => {
                                  if (e.key === "Enter" && !e.shiftKey) {
                                    e.preventDefault();
                                    handleReply(message.id);
                                  }
                                  if (e.key === "Escape") setOpenReplyId(null);
                                }}
                              />
                              <Button
                                type="button"
                                variant="warm"
                                size="sm"
                                className="h-8 px-3 text-xs"
                                onClick={() => handleReply(message.id)}
                              >
                                回复
                              </Button>
                            </div>
                          ) : (
                            <button
                              type="button"
                              onClick={() => setOpenReplyId(message.id)}
                              className="text-xs text-warm-500 hover:text-warm-700 hover:underline"
                            >
                              + 回复此留言
                            </button>
                          )}
                        </div>
                      )}
                    </div>
                  </div>
                </article>
              ))}
            </div>

            {/* ── 底部输入栏（固定） ── */}
            <div className="border-t border-border/60 bg-white/90 px-4 py-3 backdrop-blur">
              {error && (
                <p className="mb-2 rounded-xl bg-red-50 px-3 py-2 text-xs text-red-600">
                  {error}
                </p>
              )}
              <div className="flex items-end gap-2">
                <textarea
                  value={content}
                  onChange={(e) => setContent(e.target.value)}
                  placeholder={
                    user
                      ? `以 ${user.displayName} 的身份留言...`
                      : "登录后可发布留言"
                  }
                  rows={1}
                  className="max-h-24 min-h-[2.5rem] flex-1 resize-none rounded-xl border border-border/80 bg-white px-3 py-2 text-sm leading-5 outline-none transition focus:border-warm-300 focus:ring-2 focus:ring-warm-100"
                  maxLength={1000}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" && !e.shiftKey) {
                      e.preventDefault();
                      handleSubmit();
                    }
                  }}
                  onInput={(e) => {
                    const el = e.currentTarget;
                    el.style.height = "auto";
                    el.style.height = Math.min(el.scrollHeight, 96) + "px";
                  }}
                />
                <Button
                  type="button"
                  variant="warm"
                  size="sm"
                  className="h-10 shrink-0 px-3"
                  onClick={handleSubmit}
                  disabled={submitting || !content.trim()}
                >
                  <Send className="h-4 w-4" />
                </Button>
              </div>
              <div className="mt-1.5 flex justify-end">
                <span className="text-[10px] text-neutral-300">
                  {content.length}/1000
                </span>
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
