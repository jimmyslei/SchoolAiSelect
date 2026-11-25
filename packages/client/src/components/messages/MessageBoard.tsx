import { useEffect, useMemo, useState } from "react";
import { MessageCircle, Send, UserRound } from "lucide-react";
import { Button } from "@/components/ui/button";
import { createMessage, createMessageReply, fetchMessages } from "@/lib/api";
import type { MessageThread, SchoolRanking, SessionUser } from "@/types/school";

interface MessageBoardProps {
  rankings: SchoolRanking[];
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

export function MessageBoard({
  rankings,
  token,
  user,
  onRequireAuth,
}: MessageBoardProps) {
  const [selectedSchoolId, setSelectedSchoolId] = useState<string>("all");
  const [messages, setMessages] = useState<MessageThread[]>([]);
  const [content, setContent] = useState("");
  const [replyDrafts, setReplyDrafts] = useState<Record<number, string>>({});
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const selectedSchool = useMemo(
    () => rankings.find((item) => item.school.id === selectedSchoolId)?.school,
    [rankings, selectedSchoolId],
  );

  useEffect(() => {
    let ignore = false;
    setLoading(true);
    setError(null);
    fetchMessages(selectedSchoolId === "all" ? undefined : selectedSchoolId)
      .then((items) => {
        if (!ignore) setMessages(items);
      })
      .catch((err) => {
        if (!ignore)
          setError(err instanceof Error ? err.message : "留言加载失败");
      })
      .finally(() => {
        if (!ignore) setLoading(false);
      });
    return () => {
      ignore = true;
    };
  }, [selectedSchoolId]);

  const handleCreateMessage = async () => {
    if (!token || !user) {
      onRequireAuth();
      return;
    }
    if (!selectedSchool || !content.trim()) return;

    const created = await createMessage(token, {
      schoolId: selectedSchool.id,
      schoolName: selectedSchool.name,
      content,
    });
    setMessages((items) => [created, ...items]);
    setContent("");
  };

  const handleCreateReply = async (messageId: number) => {
    if (!token || !user) {
      onRequireAuth();
      return;
    }
    const replyContent = replyDrafts[messageId]?.trim();
    if (!replyContent) return;

    const reply = await createMessageReply(token, messageId, replyContent);
    setMessages((items) =>
      items.map((message) =>
        message.id === messageId
          ? { ...message, replies: [...message.replies, reply] }
          : message,
      ),
    );
    setReplyDrafts((drafts) => ({ ...drafts, [messageId]: "" }));
  };

  return (
    <section className="surface-elevated rounded-4xl p-5 sm:p-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="text-sm font-medium uppercase tracking-[0.22em] text-warm-600">
            Parent Notes
          </p>
          <h2 className="mt-2 font-display text-2xl font-semibold text-neutral-900">
            家长留言
          </h2>
          <p className="mt-2 text-sm leading-6 text-neutral-600">
            留言放在结果页，便于围绕具体学校沉淀就读体验、开放日信息与补充提醒。
          </p>
        </div>
        <div className="rounded-full bg-sage-100 p-3 text-sage-700">
          <MessageCircle className="h-5 w-5" />
        </div>
      </div>

      <div className="mt-5 flex gap-2 overflow-x-auto pb-1">
        <button
          type="button"
          onClick={() => setSelectedSchoolId("all")}
          className={`shrink-0 rounded-full px-4 py-2 text-sm transition-colors ${selectedSchoolId === "all" ? "bg-warm-500 text-white" : "bg-background/80 text-neutral-600 hover:bg-warm-50"}`}
        >
          全部学校
        </button>
        {rankings.slice(0, 8).map((item) => (
          <button
            key={item.school.id}
            type="button"
            onClick={() => setSelectedSchoolId(item.school.id)}
            className={`shrink-0 rounded-full px-4 py-2 text-sm transition-colors ${selectedSchoolId === item.school.id ? "bg-warm-500 text-white" : "bg-background/80 text-neutral-600 hover:bg-warm-50"}`}
          >
            {item.school.name}
          </button>
        ))}
      </div>

      <div className="mt-5 rounded-3xl border border-border/70 bg-background/70 p-4">
        {selectedSchool ? (
          <>
            <textarea
              value={content}
              onChange={(event) => setContent(event.target.value)}
              placeholder={
                user
                  ? `以 ${user.displayName} 的身份分享 ${selectedSchool.name} 的信息...`
                  : "登录后可发布学校留言"
              }
              className="min-h-24 w-full resize-none rounded-2xl border border-border/80 bg-card/80 px-4 py-3 text-sm leading-6 outline-none transition focus:border-warm-300 focus:ring-2 focus:ring-warm-100"
              maxLength={1000}
            />
            <div className="mt-3 flex items-center justify-between gap-3">
              <span className="text-xs text-neutral-500">
                请选择具体学校后发布留言。
              </span>
              <Button
                type="button"
                variant="warm"
                size="sm"
                onClick={handleCreateMessage}
                disabled={!content.trim()}
              >
                <Send className="h-4 w-4" />
                发布
              </Button>
            </div>
          </>
        ) : (
          <div className="py-4 text-center text-sm text-neutral-500">
            选择上方某所学校后，可发布针对该校的留言。
          </div>
        )}
      </div>

      {error && (
        <p className="mt-4 rounded-2xl bg-red-50 px-4 py-3 text-sm text-red-600">
          {error}
        </p>
      )}

      <div className="mt-5 space-y-4">
        {loading && <p className="text-sm text-neutral-500">正在加载留言...</p>}
        {!loading && messages.length === 0 && (
          <p className="rounded-2xl bg-sand-100 px-4 py-6 text-center text-sm text-neutral-500">
            暂无留言，欢迎分享第一条真实经验。
          </p>
        )}
        {messages.map((message) => (
          <article
            key={message.id}
            className="rounded-3xl border border-border/70 bg-card/70 p-4"
          >
            <div className="flex items-start gap-3">
              <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-warm-100 text-warm-700">
                <UserRound className="h-4 w-4" />
              </div>
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                  <span className="font-medium text-neutral-900">
                    {message.authorName}
                  </span>
                  <span className="rounded-full bg-sage-50 px-2 py-0.5 text-xs text-sage-700">
                    {message.schoolName}
                  </span>
                  <span className="text-xs text-neutral-400">
                    {formatDate(message.createdAt)}
                  </span>
                </div>
                <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-neutral-700">
                  {message.content}
                </p>

                <div className="mt-3 space-y-2">
                  {message.replies.map((reply) => (
                    <div
                      key={reply.id}
                      className="rounded-2xl bg-sand-50 px-3 py-2 text-sm text-neutral-600"
                    >
                      <span className="font-medium text-neutral-800">
                        {reply.authorName}
                      </span>
                      <span className="mx-2 text-xs text-neutral-400">
                        {formatDate(reply.createdAt)}
                      </span>
                      <p className="mt-1 whitespace-pre-wrap leading-6">
                        {reply.content}
                      </p>
                    </div>
                  ))}
                </div>

                <div className="mt-3 flex gap-2">
                  <input
                    value={replyDrafts[message.id] ?? ""}
                    onChange={(event) =>
                      setReplyDrafts((drafts) => ({
                        ...drafts,
                        [message.id]: event.target.value,
                      }))
                    }
                    placeholder={user ? "回复这条留言" : "登录后可回复"}
                    className="h-10 min-w-0 flex-1 rounded-full border border-border/80 bg-background/80 px-4 text-sm outline-none transition focus:border-warm-300 focus:ring-2 focus:ring-warm-100"
                    maxLength={1000}
                  />
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => handleCreateReply(message.id)}
                  >
                    回复
                  </Button>
                </div>
              </div>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}
