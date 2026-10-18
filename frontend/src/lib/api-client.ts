import { ApiClientError } from "@/lib/api-errors";
import { getApiBaseUrl } from "@/lib/api-base-url";
import { outcomeForStatus, reportApiOutcome } from "@/lib/api-status";
import type {
  ApiError,
} from "@/types/api";
import type {
  AuthTokensDto,
  RegisterRequest,
  LoginRequest,
  UserInfoDto,
  DiscordAuthUrlDto,
} from "@/types/auth";
import type {
  CharacterProfileDto,
  CreateProfileRequest,
  UpdateProfileRequest,
  ImportLodestoneRequest,
  LodestonePreviewDto,
} from "@/types/profile";
import type {
  SavedListSummaryDto,
  SavedListDto,
  FavoriteRecipeDto,
  FavoriteStatusDto,
} from "@/types/saved-lists";
import type {
  SubmitRotationRequest,
  RotationDetail,
  RotationListResult,
  RotationSearchParams,
  UpvoteResult,
} from "@/types/solver";

export { ApiClientError };

export interface RequestOptions extends RequestInit {
  /** Overrides the default request timeout; the Lodestone endpoints wait on a slow upstream. */
  timeoutMs?: number;
}

const DEFAULT_TIMEOUT_MS = 10_000;
/** The Lodestone endpoints scrape Square Enix's site, which can take most of a minute. */
export const LODESTONE_TIMEOUT_MS = 60_000;

// In-flight GET deduplication, keyed by path plus a fingerprint of the bearer token so two
// identities (or a request before and after a sign-in) never share a response. Emptied whenever
// the signed-in identity changes.
const inflightGets = new Map<string, Promise<unknown>>();

export function clearInflightRequests(): void {
  inflightGets.clear();
}

function dedupKey(path: string, init?: RequestOptions): string {
  const headers = (init?.headers as Record<string, string> | undefined) ?? {};
  const bearer = headers["Authorization"];
  // The tail of the token is enough to tell two tokens apart without holding the token in the key.
  return bearer ? `${path}|${bearer.slice(-16)}` : path;
}

async function fetchApi<T>(path: string, init?: RequestOptions): Promise<T> {
  const method = init?.method?.toUpperCase() ?? "GET";
  // Only deduplicate GETs without a caller-provided abort signal. Requests with signals manage
  // their own lifecycle (hooks with AbortControllers) and must not receive a promise that
  // another controller may abort; React Strict Mode's double mount would otherwise leave a
  // permanent loading state.
  if (method === "GET" && !init?.signal) {
    const key = dedupKey(path, init);
    const existing = inflightGets.get(key);
    if (existing) return existing as Promise<T>;

    const promise = fetchApiInternal<T>(path, init).finally(() => {
      inflightGets.delete(key);
    });
    inflightGets.set(key, promise);
    return promise;
  }
  return fetchApiInternal<T>(path, init);
}

/** The caller's signal and the timeout's as one; AbortSignal.any is not in every browser yet. */
function combineSignals(first: AbortSignal, second: AbortSignal): AbortSignal {
  if (typeof AbortSignal.any === "function") return AbortSignal.any([first, second]);

  const controller = new AbortController();
  for (const signal of [first, second]) {
    if (signal.aborted) {
      controller.abort(signal.reason);
      break;
    }
    signal.addEventListener("abort", () => controller.abort(signal.reason), { once: true });
  }
  return controller.signal;
}

async function fetchApiInternal<T>(
  path: string,
  init?: RequestOptions,
): Promise<T> {
  const url = `${getApiBaseUrl()}${path}`;
  const { timeoutMs = DEFAULT_TIMEOUT_MS, signal: callerSignal, ...rest } = init ?? {};

  const timeoutController = new AbortController();
  const timeout = setTimeout(() => timeoutController.abort(), timeoutMs);
  const signal = callerSignal
    ? combineSignals(callerSignal, timeoutController.signal)
    : timeoutController.signal;

  try {
    const response = await fetch(url, {
      ...rest,
      signal,
      headers: {
        Accept: "application/json",
        ...rest.headers,
      },
    });

    // Any answer means the API is up, even a 4xx; a gateway error means it is not.
    reportApiOutcome(outcomeForStatus(response.status));

    if (!response.ok) {
      const body = (await response.json().catch(() => ({
        error: `HTTP ${response.status}: ${response.statusText}`,
      }))) as ApiError;
      throw new ApiClientError(response.status, body);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return (await response.json()) as T;
  } catch (err) {
    if (err instanceof ApiClientError) throw err;

    // The caller cancelled: their AbortError passes through untouched.
    if (callerSignal?.aborted) throw err;

    // Ours: the timeout fired. Surfaced as an error the UI can show, never as an AbortError the
    // hooks would mistake for their own cancellation (and swallow into a permanent spinner).
    if (timeoutController.signal.aborted) {
      reportApiOutcome("unreachable");
      throw new ApiClientError(408, { error: "The request timed out. Please try again." });
    }

    // fetch() itself failed: offline, DNS, CORS, or an API that is not answering at all.
    reportApiOutcome("unreachable");
    throw new ApiClientError(0, {
      error: "Could not reach the server. Check your connection and try again.",
    });
  } finally {
    clearTimeout(timeout);
  }
}

// --- Auth interceptor wiring ---

type TokenProvider = () => string | null;
type OnUnauthorized = () => void;
type RefreshFn = () => Promise<boolean>;

let _getToken: TokenProvider = () => null;
let _onUnauthorized: OnUnauthorized = () => {};
let _refreshTokens: RefreshFn | null = null;

export function configureAuth(
  getToken: TokenProvider,
  onUnauthorized: OnUnauthorized,
  refreshTokens: RefreshFn,
) {
  _getToken = getToken;
  _onUnauthorized = onUnauthorized;
  _refreshTokens = refreshTokens;
}

async function authFetchApi<T>(path: string, init?: RequestOptions): Promise<T> {
  const token = _getToken();
  const headers: Record<string, string> = {
    ...((init?.headers as Record<string, string>) ?? {}),
  };
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  try {
    return await fetchApi<T>(path, { ...init, headers });
  } catch (err) {
    if (err instanceof ApiClientError && err.status === 401 && _refreshTokens) {
      const refreshed = await _refreshTokens();
      if (refreshed) {
        const newToken = _getToken();
        if (newToken) {
          headers["Authorization"] = `Bearer ${newToken}`;
          return fetchApi<T>(path, { ...init, headers });
        }
      }
      _onUnauthorized();
    }
    throw err;
  }
}

// --- Auth API ---

export function authRegister(req: RegisterRequest): Promise<AuthTokensDto> {
  return fetchApi<AuthTokensDto>("/api/auth/register", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  });
}

export function authLogin(req: LoginRequest): Promise<AuthTokensDto> {
  return fetchApi<AuthTokensDto>("/api/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  });
}

export function authRefresh(refreshToken: string): Promise<AuthTokensDto> {
  return fetchApi<AuthTokensDto>("/api/auth/refresh", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  });
}

export function authLogout(refreshToken: string): Promise<void> {
  return authFetchApi<void>("/api/auth/logout", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  });
}

export function authGetMe(): Promise<UserInfoDto> {
  return authFetchApi<UserInfoDto>("/api/auth/me");
}

export function authGetDiscordUrl(): Promise<DiscordAuthUrlDto> {
  return fetchApi<DiscordAuthUrlDto>("/api/auth/discord/url");
}

export function authDiscordCallback(code: string, state: string): Promise<AuthTokensDto> {
  return fetchApi<AuthTokensDto>("/api/auth/discord/callback", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ code, state }),
  });
}

// --- Profile API ---

export function getProfile(signal?: AbortSignal): Promise<CharacterProfileDto> {
  return authFetchApi<CharacterProfileDto>("/api/profile", { signal });
}

export function createProfile(
  req: CreateProfileRequest,
): Promise<CharacterProfileDto> {
  return authFetchApi<CharacterProfileDto>("/api/profile", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  });
}

export function updateProfile(
  req: UpdateProfileRequest,
): Promise<CharacterProfileDto> {
  return authFetchApi<CharacterProfileDto>("/api/profile", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  });
}

export function updateJobLevels(
  jobLevels: Record<string, number>,
): Promise<CharacterProfileDto> {
  return authFetchApi<CharacterProfileDto>("/api/profile/jobs", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ jobLevels }),
  });
}

export function deleteProfile(): Promise<void> {
  return authFetchApi<void>("/api/profile", { method: "DELETE" });
}

export function importLodestone(
  req: ImportLodestoneRequest,
): Promise<CharacterProfileDto> {
  return authFetchApi<CharacterProfileDto>("/api/profile/import-lodestone", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
    timeoutMs: LODESTONE_TIMEOUT_MS,
  });
}

export function refreshLodestone(): Promise<CharacterProfileDto> {
  return authFetchApi<CharacterProfileDto>("/api/profile/refresh-lodestone", {
    method: "POST",
    timeoutMs: LODESTONE_TIMEOUT_MS,
  });
}

export function previewLodestone(
  name: string,
  server: string,
  signal?: AbortSignal,
): Promise<LodestonePreviewDto> {
  const params = new URLSearchParams({ name, server });
  return fetchApi<LodestonePreviewDto>(
    `/api/profile/lodestone-preview?${params}`,
    { signal, timeoutMs: LODESTONE_TIMEOUT_MS },
  );
}

// --- Saved Lists API ---

export function getSavedLists(
  type?: string,
  signal?: AbortSignal,
): Promise<SavedListSummaryDto[]> {
  const params = type ? `?type=${type}` : "";
  return authFetchApi<SavedListSummaryDto[]>(`/api/saved-lists${params}`, {
    signal,
  });
}

export function getSavedListById(id: number): Promise<SavedListDto> {
  return authFetchApi<SavedListDto>(`/api/saved-lists/${id}`);
}

export function updateSavedList(
  id: number,
  req: { name?: string; isPinned?: boolean },
): Promise<SavedListDto> {
  return authFetchApi<SavedListDto>(`/api/saved-lists/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  });
}

export function deleteSavedList(id: number): Promise<void> {
  return authFetchApi<void>(`/api/saved-lists/${id}`, { method: "DELETE" });
}

export function addItemToSavedList(
  listId: number,
  req: {
    itemId: number;
    itemName: string;
    quantity: number;
    category?: string;
  },
): Promise<SavedListDto> {
  return authFetchApi<SavedListDto>(`/api/saved-lists/${listId}/items`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  });
}

export function removeItemFromSavedList(
  listId: number,
  itemId: number,
): Promise<SavedListDto> {
  return authFetchApi<SavedListDto>(
    `/api/saved-lists/${listId}/items/${itemId}`,
    {
      method: "DELETE",
    },
  );
}

// --- Favorites API ---

export function getFavoriteRecipes(
  signal?: AbortSignal,
): Promise<FavoriteRecipeDto[]> {
  return authFetchApi<FavoriteRecipeDto[]>("/api/favorites/recipes", {
    signal,
  });
}

export function toggleFavoriteRecipe(
  recipeId: number,
  note?: string,
): Promise<FavoriteStatusDto> {
  return authFetchApi<FavoriteStatusDto>(`/api/favorites/recipes/${recipeId}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ note: note ?? null }),
  });
}

// --- Gear Comparison API ---

export function getGearComparison(
  job: string,
  level: number,
  profile: string,
  signal?: AbortSignal,
): Promise<import("@/types/gear-comparison").GearComparisonDto> {
  const params = new URLSearchParams({
    job,
    level: String(level),
    profile,
  });
  return authFetchApi(`/api/gear/recommendations/compare?${params}`, {
    signal,
  });
}

// --- Profile Gear Update ---

export function updateCurrentGear(
  slots: Record<string, number>,
): Promise<CharacterProfileDto> {
  return authFetchApi<CharacterProfileDto>("/api/profile/gear", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ slots }),
  });
}

// --- Save Actions ---

export function saveShoppingList(req: {
  name: string;
  recipeId: number;
  quantity: number;
  notes?: string;
}): Promise<SavedListDto> {
  return authFetchApi<SavedListDto>("/api/saved-lists/shopping-list", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  });
}

export function saveGearSet(req: {
  name: string;
  job: string;
  slots: Record<string, number>;
}): Promise<SavedListDto> {
  return authFetchApi<SavedListDto>("/api/saved-lists/gear-set", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  });
}

// --- Rotation Library API ---

export function submitRotation(
  request: SubmitRotationRequest,
): Promise<RotationDetail> {
  return authFetchApi<RotationDetail>("/api/rotations", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
}

export function searchRotations(
  params: RotationSearchParams,
  signal?: AbortSignal,
): Promise<RotationListResult> {
  const search = new URLSearchParams();
  if (params.recipeId) search.set("recipeId", String(params.recipeId));
  if (params.craftType) search.set("craftType", params.craftType);
  if (params.minLevel) search.set("minLevel", String(params.minLevel));
  if (params.maxLevel) search.set("maxLevel", String(params.maxLevel));
  if (params.source) search.set("source", params.source);
  if (params.sortBy) search.set("sortBy", params.sortBy);
  search.set("page", String(params.page ?? 1));
  search.set("pageSize", String(params.pageSize ?? 20));
  return fetchApi<RotationListResult>(`/api/rotations?${search}`, { signal });
}

export function getRotationById(
  id: number,
  signal?: AbortSignal,
): Promise<RotationDetail> {
  return fetchApi<RotationDetail>(`/api/rotations/${id}`, { signal });
}

export function toggleRotationUpvote(id: number): Promise<UpvoteResult> {
  return authFetchApi<UpvoteResult>(`/api/rotations/${id}/upvote`, {
    method: "POST",
  });
}

export function deleteRotation(id: number): Promise<void> {
  return authFetchApi<void>(`/api/rotations/${id}`, { method: "DELETE" });
}

// --- Craft Plans API ---

export function createCraftPlan(
  name: string,
  items: import("@/types/craft-plan").CraftPlanItemInput[],
): Promise<import("@/types/craft-plan").CraftPlanDto> {
  return authFetchApi("/api/craft-plans", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ name, items }),
  });
}

export function getCraftPlans(
  signal?: AbortSignal,
): Promise<import("@/types/craft-plan").CraftPlanDto[]> {
  return authFetchApi("/api/craft-plans", { signal });
}

export function getCraftPlanById(
  id: string,
  signal?: AbortSignal,
): Promise<import("@/types/craft-plan").CraftPlanDto> {
  return authFetchApi(`/api/craft-plans/${encodeURIComponent(id)}`, { signal });
}

/** A shared plan's name and items; the only account call a shareable plan link needs. */
export function getSharedCraftPlan(
  id: string,
  signal?: AbortSignal,
): Promise<import("@/types/craft-plan").CraftPlanDto> {
  return fetchApi(`/api/craft-plans/${encodeURIComponent(id)}/items`, { signal });
}

export function deleteCraftPlan(id: string): Promise<void> {
  return authFetchApi(`/api/craft-plans/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
}

// --- Notifications API ---

export function getNotifications(
  signal?: AbortSignal,
): Promise<import("@/types/notifications").NotificationDto[]> {
  return authFetchApi("/api/notifications", { signal });
}

export function markNotificationRead(id: string): Promise<void> {
  return authFetchApi(`/api/notifications/${encodeURIComponent(id)}/read`, {
    method: "POST",
  });
}

export function markAllNotificationsRead(): Promise<void> {
  return authFetchApi("/api/notifications/read-all", {
    method: "POST",
  });
}

// --- Feedback API ---

export function submitFeedback(request: {
  category: string;
  message: string;
  email?: string;
  pageUrl?: string;
}): Promise<{ id: number; createdAt: string }> {
  return fetchApi("/api/feedback", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
}
