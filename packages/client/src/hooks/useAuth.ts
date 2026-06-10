import { useCallback, useEffect, useRef, useState } from "react";
import { getCurrentUser, loginWithEmail, registerWithEmail } from "@/lib/api";
import type { AuthPayload, SessionUser } from "@/types/school";

const STORAGE_KEY = "school-recommender-auth";

export function useAuth() {
  const [token, setToken] = useState<string | null>(null);
  const [user, setUser] = useState<SessionUser | null>(null);
  const [authChecked, setAuthChecked] = useState(false);
  const validatingRef = useRef(false);

  useEffect(() => {
    try {
      const saved = window.localStorage.getItem(STORAGE_KEY);
      if (!saved) {
        setAuthChecked(true);
        return;
      }
      const payload = JSON.parse(saved) as AuthPayload;
      if (payload.token && payload.user) {
        // 先乐观设置，再向后端验证
        setToken(payload.token);
        setUser(payload.user);

        if (!validatingRef.current) {
          validatingRef.current = true;
          getCurrentUser(payload.token)
            .then((serverUser) => {
              if (!serverUser) {
                // 后端不认可该 token，清除本地状态
                window.localStorage.removeItem(STORAGE_KEY);
                setToken(null);
                setUser(null);
              }
            })
            .catch(() => {
              // 网络错误时保留本地状态，避免误清
            })
            .finally(() => {
              setAuthChecked(true);
            });
        }
      } else {
        setAuthChecked(true);
      }
    } catch {
      window.localStorage.removeItem(STORAGE_KEY);
      setAuthChecked(true);
    }
  }, []);

  const persist = useCallback((payload: AuthPayload) => {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
    setToken(payload.token);
    setUser(payload.user);
  }, []);

  const register = useCallback(
    async (
      email: string,
      password: string,
      displayName: string,
      verificationCode: string,
    ) => {
      const payload = await registerWithEmail({
        email,
        password,
        displayName,
        verificationCode,
      });
      persist(payload);
    },
    [persist],
  );

  const login = useCallback(
    async (email: string, password: string) => {
      const payload = await loginWithEmail({ email, password });
      persist(payload);
    },
    [persist],
  );

  const logout = useCallback(() => {
    window.localStorage.removeItem(STORAGE_KEY);
    setToken(null);
    setUser(null);
  }, []);

  /** 外部调用：当接口返回 401 时清除登录状态 */
  const clearAuth = useCallback(() => {
    window.localStorage.removeItem(STORAGE_KEY);
    setToken(null);
    setUser(null);
  }, []);

  return {
    token,
    user,
    isAuthenticated: Boolean(token && user),
    authChecked,
    register,
    login,
    logout,
    clearAuth,
  };
}
