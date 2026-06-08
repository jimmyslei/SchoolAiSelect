import { useState } from "react";
import { Mail, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { sendRegisterCode } from "@/lib/api";

type AuthMode = "login" | "register";

interface AuthDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onLogin: (email: string, password: string) => Promise<void>;
  onRegister: (
    email: string,
    password: string,
    displayName: string,
    verificationCode: string,
  ) => Promise<void>;
}

export function AuthDialog({
  open,
  onOpenChange,
  onLogin,
  onRegister,
}: AuthDialogProps) {
  const [mode, setMode] = useState<AuthMode>("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [verificationCode, setVerificationCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [sendingCode, setSendingCode] = useState(false);
  const [cooldown, setCooldown] = useState(0);

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      if (mode === "register") {
        await onRegister(email, password, displayName, verificationCode);
      } else {
        await onLogin(email, password);
      }
      onOpenChange(false);
      setPassword("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "操作失败，请稍后重试");
    } finally {
      setSubmitting(false);
    }
  };

  const handleSendCode = async () => {
    setError(null);
    setNotice(null);
    setSendingCode(true);
    try {
      await sendRegisterCode(email);
      setNotice("验证码已发送，请查看邮箱。10 分钟内有效。");
      setCooldown(60);
      const timer = window.setInterval(() => {
        setCooldown((value) => {
          if (value <= 1) {
            window.clearInterval(timer);
            return 0;
          }
          return value - 1;
        });
      }, 1000);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "验证码发送失败，请稍后重试",
      );
    } finally {
      setSendingCode(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <div className="mb-2 inline-flex h-12 w-12 items-center justify-center rounded-full bg-warm-100 text-warm-700">
            {mode === "register" ? (
              <ShieldCheck className="h-5 w-5" />
            ) : (
              <Mail className="h-5 w-5" />
            )}
          </div>
          <DialogTitle>
            {mode === "register" ? "创建家长账号" : "邮箱登录"}
          </DialogTitle>
          <DialogDescription>
            {mode === "register"
              ? "先获取邮箱验证码，再完成账号注册。"
              : "登录后可在学校结果页发布留言、回复其他家长经验。"}
          </DialogDescription>
        </DialogHeader>

        <form className="space-y-4" onSubmit={handleSubmit}>
          {mode === "register" && (
            <Input
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
              placeholder="昵称，例如：乐乐妈妈"
              autoComplete="nickname"
              required
            />
          )}
          <Input
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            placeholder="邮箱地址"
            autoComplete="email"
            required
          />
          <Input
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            placeholder="密码，至少 8 位"
            autoComplete={
              mode === "register" ? "new-password" : "current-password"
            }
            required
            minLength={8}
          />

          {mode === "register" && (
            <div className="space-y-3 rounded-3xl border border-warm-100 bg-warm-50/70 p-3">
              <div className="flex gap-2">
                <Input
                  value={verificationCode}
                  onChange={(event) =>
                    setVerificationCode(
                      event.target.value.replace(/\D/g, "").slice(0, 6),
                    )
                  }
                  placeholder="输入 6 位邮箱验证码"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  required
                  className="tracking-[0.2em]"
                />
                <Button
                  type="button"
                  variant="outline"
                  className="h-12 shrink-0 px-4"
                  disabled={sendingCode || cooldown > 0 || !email}
                  onClick={handleSendCode}
                >
                  {sendingCode
                    ? "发送中"
                    : cooldown > 0
                      ? `${cooldown}s`
                      : "发送验证码"}
                </Button>
              </div>
              <p className="text-xs leading-5 text-warm-700">
                验证码邮件为暖色卡片样式，包含醒目的 6 位数字码和 10
                分钟有效期提示。
              </p>
            </div>
          )}

          {notice && (
            <p className="rounded-2xl bg-sage-50 px-4 py-3 text-sm text-sage-700">
              {notice}
            </p>
          )}
          {error && (
            <p className="rounded-2xl bg-red-50 px-4 py-3 text-sm text-red-600">
              {error}
            </p>
          )}

          <Button
            type="submit"
            variant="warm"
            className="w-full"
            disabled={submitting}
          >
            {submitting
              ? "提交中..."
              : mode === "register"
                ? "注册并登录"
                : "登录"}
          </Button>
        </form>

        <button
          type="button"
          onClick={() => {
            setMode(mode === "register" ? "login" : "register");
            setError(null);
            setNotice(null);
          }}
          className="text-center text-sm text-neutral-500 transition-colors hover:text-warm-700"
        >
          {mode === "register"
            ? "已有账号？去登录"
            : "还没有账号？使用邮箱注册"}
        </button>
      </DialogContent>
    </Dialog>
  );
}
