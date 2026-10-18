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
  clearInflightRequests,
  ApiClientError,
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
  handleDiscordCallback: (code: string, state: string) => Promise<void>;
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
const OAUTH_STATE_KEY = "ffxiv_oauth_state";
const REFRESH_LOCK_NAME = "ffxiv-token-refresh";
const DISCORD_ORIGIN = "https://discord.com";

/** Thrown when a Discord callback does not match the sign-in this browser started. */
export class OAuthStateError extends Error {
  constructor() {
    super("This sign-in link is stale or was not started from this browser. Please try again.");
    this.name = "OAuthStateError";
  }
}

// Module-level token storage — NOT in React state for security
let accessToken: string | null = null;

function readRefreshToken(): string | null {
  try {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  } catch {
    return null;
  }
}

function writeRefreshToken(token: string | null): void {
  try {
    if (token) localStorage.setItem(REFRESH_TOKEN_KEY, token);
    else localStorage.removeItem(REFRESH_TOKEN_KEY);
  } catch {
    // Storage unavailable: the session lasts as long as the access token.
  }
}

/**
 * Refresh tokens are single use, so two tabs refreshing the same stored token at once would log
 * one of them out. With Web Locks the refresh runs one tab at a time and re-reads the stored
 * token after acquiring the lock; without them, the in-flight promise still dedupes within a tab.
 */
async function withRefreshLock<T>(work: () => Promise<T>): Promise<T> {
  const locks = typeof navigator !== "undefined" ? navigator.locks : undefined;
  if (!locks) return work();
  return locks.request(REFRESH_LOCK_NAME, work);
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<UserInfoDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const refreshingRef = useRef<Promise<boolean> | null>(null);

  const refreshTokens = useCallback(async (): Promise<boolean> => {
    if (refreshingRef.current) return refreshingRef.current;

    const promise = withRefreshLock(async () => {
      // Another tab may have rotated the token while this one waited for the lock; the stored
      // value is the only one the API still accepts.
      const storedRefresh = readRefreshToken();
      if (!storedRefresh) return false;

      try {
        const tokens = await authRefresh(storedRefresh);
        accessToken = tokens.accessToken;
        writeRefreshToken(tokens.refreshToken);
        clearInflightRequests();
        return true;
      } catch (err) {
        // Only the API rejecting the token ends the session. An API that is unreachable or
        // failing keeps the token for the next attempt instead of signing the user out.
        if (err instanceof ApiClientError && (err.status === 400 || err.status === 401)) {
          accessToken = null;
          writeRefreshToken(null);
        }
        return false;
      }
    }).finally(() => {
      refreshingRef.current = null;
    });

    refreshingRef.current = promise;
    return promise;
  }, []);

  const getToken = useCallback((): string | null => {
    return accessToken;
  }, []);

  const clearSession = useCallback(() => {
    accessToken = null;
    writeRefreshToken(null);
    clearInflightRequests();
    setUser(null);
  }, []);

  // Wire up auth interceptor in api-client
  useEffect(() => {
    configureAuth(getToken, clearSession, refreshTokens);
  }, [getToken, clearSession, refreshTokens]);

  // Silent refresh on mount, only when a session was left behind: a visitor who never signed
  // in makes no request to the API.
  useEffect(() => {
    (async () => {
      const refreshed = await refreshTokens();
      if (refreshed) {
        try {
          const userInfo = await authGetMe();
          setUser(userInfo);
        } catch (err) {
          if (err instanceof ApiClientError && err.status === 401) clearSession();
        }
      }
      setIsLoading(false);
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Other tabs: a sign-out removes the stored token and ends this tab's session too; a sign-in
  // elsewhere is adopted so the tabs agree on who is signed in.
  useEffect(() => {
    const handleStorage = (event: StorageEvent) => {
      if (event.key !== REFRESH_TOKEN_KEY) return;
      if (event.newValue === null) {
        accessToken = null;
        clearInflightRequests();
        setUser(null);
        return;
      }
      if (event.oldValue === null && accessToken === null) {
        refreshTokens().then((refreshed) => {
          if (refreshed) authGetMe().then(setUser).catch(() => {});
        });
      }
    };
    window.addEventListener("storage", handleStorage);
    return () => window.removeEventListener("storage", handleStorage);
  }, [refreshTokens]);

  const handleTokens = useCallback(
    async (tokens: { accessToken: string; refreshToken: string; expiresAt: string }) => {
      accessToken = tokens.accessToken;
      writeRefreshToken(tokens.refreshToken);
      clearInflightRequests();
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
    const { authorizationUrl, state } = await authGetDiscordUrl();
    // The browser only ever follows the API to Discord itself.
    const url = new URL(authorizationUrl);
    if (url.origin !== DISCORD_ORIGIN) {
      throw new Error("Unexpected authorization URL.");
    }
    sessionStorage.setItem(OAUTH_STATE_KEY, state);
    window.location.assign(url.toString());
  }, []);

  const handleDiscordCallback = useCallback(
    async (code: string, state: string) => {
      const expected = sessionStorage.getItem(OAUTH_STATE_KEY);
      sessionStorage.removeItem(OAUTH_STATE_KEY);
      if (!state || !expected || state !== expected) {
        throw new OAuthStateError();
      }
      const tokens = await authDiscordCallback(code, state);
      await handleTokens(tokens);
    },
    [handleTokens],
  );

  const logout = useCallback(async () => {
    const refreshToken = readRefreshToken();
    if (accessToken && refreshToken) {
      try {
        await authLogout(refreshToken);
      } catch {
        // Best effort
      }
    }
    clearSession();
  }, [clearSession]);

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
