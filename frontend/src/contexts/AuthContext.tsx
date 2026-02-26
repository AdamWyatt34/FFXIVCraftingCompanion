"use client";

import {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
  useRef,
} from "react";
import {
  authRegister,
  authLogin,
  authRefresh,
  authLogout,
  authGetMe,
  authGetDiscordUrl,
  authDiscordCallback,
  configureAuth,
} from "@/lib/api-client";
import type { UserInfoDto } from "@/types/auth";
import type { LoginRequest, RegisterRequest } from "@/types/auth";

interface AuthContextValue {
  user: UserInfoDto | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (req: LoginRequest) => Promise<void>;
  register: (req: RegisterRequest) => Promise<void>;
  loginWithDiscord: () => Promise<void>;
  handleDiscordCallback: (code: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}

const REFRESH_TOKEN_KEY = "ffxiv_refresh_token";

// Module-level token storage — NOT in React state for security
let accessToken: string | null = null;

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<UserInfoDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const refreshingRef = useRef<Promise<boolean> | null>(null);

  const refreshTokens = useCallback(async (): Promise<boolean> => {
    if (refreshingRef.current) return refreshingRef.current;

    const promise = (async () => {
      const storedRefresh = localStorage.getItem(REFRESH_TOKEN_KEY);
      if (!storedRefresh) return false;

      try {
        const tokens = await authRefresh(storedRefresh);
        accessToken = tokens.accessToken;

        localStorage.setItem(REFRESH_TOKEN_KEY, tokens.refreshToken);
        return true;
      } catch {
        accessToken = null;
    
        localStorage.removeItem(REFRESH_TOKEN_KEY);
        return false;
      } finally {
        refreshingRef.current = null;
      }
    })();

    refreshingRef.current = promise;
    return promise;
  }, []);

  const getToken = useCallback((): string | null => {
    return accessToken;
  }, []);

  const onUnauthorized = useCallback(() => {
    accessToken = null;

    localStorage.removeItem(REFRESH_TOKEN_KEY);
    setUser(null);
  }, []);

  // Wire up auth interceptor in api-client
  useEffect(() => {
    configureAuth(getToken, onUnauthorized, refreshTokens);
  }, [getToken, onUnauthorized, refreshTokens]);

  // Silent refresh on mount
  useEffect(() => {
    (async () => {
      const refreshed = await refreshTokens();
      if (refreshed) {
        try {
          const userInfo = await authGetMe();
          setUser(userInfo);
        } catch {
          onUnauthorized();
        }
      }
      setIsLoading(false);
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleTokens = useCallback(
    async (tokens: { accessToken: string; refreshToken: string; expiresAt: string }) => {
      accessToken = tokens.accessToken;
      tokenExpiresAt = new Date(tokens.expiresAt).getTime();
      localStorage.setItem(REFRESH_TOKEN_KEY, tokens.refreshToken);
      const userInfo = await authGetMe();
      setUser(userInfo);
    },
    [],
  );

  const login = useCallback(
    async (req: LoginRequest) => {
      const tokens = await authLogin(req);
      await handleTokens(tokens);
    },
    [handleTokens],
  );

  const register = useCallback(
    async (req: RegisterRequest) => {
      const tokens = await authRegister(req);
      await handleTokens(tokens);
    },
    [handleTokens],
  );

  const loginWithDiscord = useCallback(async () => {
    const { authorizationUrl } = await authGetDiscordUrl();
    window.location.href = authorizationUrl;
  }, []);

  const handleDiscordCallback = useCallback(
    async (code: string) => {
      const tokens = await authDiscordCallback(code);
      await handleTokens(tokens);
    },
    [handleTokens],
  );

  const logout = useCallback(async () => {
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
    if (accessToken && refreshToken) {
      try {
        await authLogout(accessToken, refreshToken);
      } catch {
        // Best effort
      }
    }
    accessToken = null;

    localStorage.removeItem(REFRESH_TOKEN_KEY);
    setUser(null);
  }, []);

  const refreshUser = useCallback(async () => {
    try {
      const userInfo = await authGetMe();
      setUser(userInfo);
    } catch {
      // Ignore
    }
  }, []);

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: user !== null,
        isLoading,
        login,
        register,
        loginWithDiscord,
        handleDiscordCallback,
        logout,
        refreshUser,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
