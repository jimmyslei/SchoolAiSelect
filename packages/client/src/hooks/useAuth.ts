import { useCallback, useEffect, useRef, useState } from "react";
import {
  getCurrentUser,
  loginWithEmail,
  registerWithEmail,
  setAuthClearCallback,
} from "@/lib/api";
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

      // 检查是否过期
      if (payload.expiresAt && new Date(payload.expiresAt) < new Date()) {
        window.localStorage.removeItem(STORAGE_KEY);
        setAuthChecked(true);
        return;
      }

      // 未过期则直接使用
      if (payload.token && payload.user) {
        setToken(payload.token);
        setUser(payload.user);
        setAuthChecked(true);
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

  // 注册 401 回调
  useEffect(() => {
    setAuthClearCallback(clearAuth);
  }, [clearAuth]);

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
