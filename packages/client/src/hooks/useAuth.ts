import { useCallback, useEffect, useState } from "react";
import { loginWithEmail, registerWithEmail } from "@/lib/api";
import type { AuthPayload, SessionUser } from "@/types/school";

const STORAGE_KEY = "school-recommender-auth";

export function useAuth() {
  const [token, setToken] = useState<string | null>(null);
  const [user, setUser] = useState<SessionUser | null>(null);

  useEffect(() => {
    try {
      const saved = window.localStorage.getItem(STORAGE_KEY);
      if (!saved) return;
      const payload = JSON.parse(saved) as AuthPayload;
      if (payload.token && payload.user) {
        setToken(payload.token);
        setUser(payload.user);
      }
    } catch {
      window.localStorage.removeItem(STORAGE_KEY);
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

  return {
    token,
    user,
    isAuthenticated: Boolean(token && user),
    register,
    login,
    logout,
  };
}
