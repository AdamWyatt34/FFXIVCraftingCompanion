import type {
  ItemDto,
  ItemSearchResultDto,
  RecipeDto,
  MaterialTreeResultDto,
  ShoppingListResultDto,
  ApiError,
} from "@/types/api";
import type {
  GearSetDto,
  GearSlotDto,
  StatWeightProfileDto,
  GearStatsDto,
  EquipmentDto,
  CalculateGearStatsRequest,
} from "@/types/gear";
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

export class ApiClientError extends Error {
  constructor(
    public status: number,
    public body: ApiError,
  ) {
    super(body.error);
    this.name = "ApiClientError";
  }
}

function getBaseUrl(): string {
  if (typeof window !== "undefined" && process.env.NODE_ENV === "development") {
    return "";
  }
  return process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5159";
}

async function fetchApi<T>(path: string, init?: RequestInit): Promise<T> {
  const url = `${getBaseUrl()}${path}`;

  const timeoutController = new AbortController();
  const timeout = setTimeout(() => timeoutController.abort(), 10_000);

  const signal = init?.signal
    ? AbortSignal.any([init.signal, timeoutController.signal])
    : timeoutController.signal;

  try {
    const response = await fetch(url, {
      ...init,
      signal,
      headers: {
        Accept: "application/json",
        ...init?.headers,
      },
    });

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

async function authFetchApi<T>(path: string, init?: RequestInit): Promise<T> {
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

// --- Items API ---

export function searchItems(
  query: string | null,
  page = 1,
  pageSize = 20,
  signal?: AbortSignal,
  craftType?: string,
): Promise<ItemSearchResultDto> {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });
  if (query) params.set("q", query);
  if (craftType) params.set("craftType", craftType);
  return fetchApi<ItemSearchResultDto>(`/api/items/search?${params}`, { signal });
}

export function getItem(id: number): Promise<ItemDto> {
  return fetchApi<ItemDto>(`/api/items/${id}`);
}

// --- Recipes API ---

export function getRecipe(
  id: number,
  includeMaterials = false,
): Promise<RecipeDto> {
  const params = includeMaterials ? "?includeMaterials=true" : "";
  return fetchApi<RecipeDto>(`/api/recipes/${id}${params}`);
}

export function getMaterialTree(
  recipeId: number,
  quantity = 1,
  signal?: AbortSignal,
): Promise<MaterialTreeResultDto> {
  const params = quantity > 1 ? `?quantity=${quantity}` : "";
  return fetchApi<MaterialTreeResultDto>(
    `/api/recipes/${recipeId}/materials${params}`,
    { signal },
  );
}

export function getShoppingList(
  recipeId: number,
  quantity = 1,
  signal?: AbortSignal,
): Promise<ShoppingListResultDto> {
  const params = quantity > 1 ? `?quantity=${quantity}` : "";
  return fetchApi<ShoppingListResultDto>(
    `/api/recipes/${recipeId}/shoppinglist${params}`,
    { signal },
  );
}

// --- Gear API ---

export function getGearRecommendations(
  job: string,
  level: number,
  profile: string,
  signal?: AbortSignal,
): Promise<GearSetDto> {
  const params = new URLSearchParams({
    job,
    level: String(level),
    profile,
  });
  return fetchApi<GearSetDto>(`/api/gear/recommendations?${params}`, { signal });
}

export function getSlotRecommendation(
  slot: string,
  job: string,
  level: number,
  profile: string,
  signal?: AbortSignal,
): Promise<GearSlotDto> {
  const params = new URLSearchParams({
    job,
    level: String(level),
    profile,
  });
  return fetchApi<GearSlotDto>(`/api/gear/recommendations/${slot}?${params}`, { signal });
}

export function getStatWeightProfiles(
  job: string,
  signal?: AbortSignal,
): Promise<StatWeightProfileDto[]> {
  const params = new URLSearchParams({ job });
  return fetchApi<StatWeightProfileDto[]>(`/api/gear/profiles?${params}`, { signal });
}

export function calculateGearStats(
  request: CalculateGearStatsRequest,
  signal?: AbortSignal,
): Promise<GearStatsDto> {
  return fetchApi<GearStatsDto>("/api/gear/calculate-stats", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
    signal,
  });
}

export function searchEquipment(
  query: string,
  job?: string,
  slot?: string,
  minLevel?: number,
  maxLevel?: number,
  signal?: AbortSignal,
): Promise<EquipmentDto[]> {
  const params = new URLSearchParams({ q: query });
  if (job) params.set("job", job);
  if (slot) params.set("slot", slot);
  if (minLevel !== undefined) params.set("minLevel", String(minLevel));
  if (maxLevel !== undefined) params.set("maxLevel", String(maxLevel));
  return fetchApi<EquipmentDto[]>(`/api/gear/search?${params}`, { signal });
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

export function authLogout(accessToken: string, refreshToken: string): Promise<void> {
  return fetchApi<void>("/api/auth/logout", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ refreshToken }),
  });
}

export function authGetMe(): Promise<UserInfoDto> {
  return authFetchApi<UserInfoDto>("/api/auth/me");
}

export function authGetDiscordUrl(): Promise<DiscordAuthUrlDto> {
  return fetchApi<DiscordAuthUrlDto>("/api/auth/discord/url");
}

export function authDiscordCallback(code: string): Promise<AuthTokensDto> {
  return fetchApi<AuthTokensDto>("/api/auth/discord/callback", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ code }),
  });
}

// --- Profile API ---

export function getProfile(signal?: AbortSignal): Promise<CharacterProfileDto> {
  return authFetchApi<CharacterProfileDto>("/api/profile", { signal });
}

export function createProfile(req: CreateProfileRequest): Promise<CharacterProfileDto> {
  return authFetchApi<CharacterProfileDto>("/api/profile", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  });
}

export function updateProfile(req: UpdateProfileRequest): Promise<CharacterProfileDto> {
  return authFetchApi<CharacterProfileDto>("/api/profile", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  });
}

export function updateJobLevels(jobLevels: Record<string, number>): Promise<CharacterProfileDto> {
  return authFetchApi<CharacterProfileDto>("/api/profile/jobs", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ jobLevels }),
  });
}

export function deleteProfile(): Promise<void> {
  return authFetchApi<void>("/api/profile", { method: "DELETE" });
}

export function importLodestone(req: ImportLodestoneRequest): Promise<CharacterProfileDto> {
  return authFetchApi<CharacterProfileDto>("/api/profile/import-lodestone", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  });
}

export function refreshLodestone(): Promise<CharacterProfileDto> {
  return authFetchApi<CharacterProfileDto>("/api/profile/refresh-lodestone", {
    method: "POST",
  });
}

export function previewLodestone(
  name: string,
  server: string,
  signal?: AbortSignal,
): Promise<LodestonePreviewDto> {
  const params = new URLSearchParams({ name, server });
  return fetchApi<LodestonePreviewDto>(`/api/profile/lodestone-preview?${params}`, { signal });
}

// --- Saved Lists API ---

export function getSavedLists(
  type?: string,
  signal?: AbortSignal,
): Promise<SavedListSummaryDto[]> {
  const params = type ? `?type=${type}` : "";
  return authFetchApi<SavedListSummaryDto[]>(`/api/saved-lists${params}`, { signal });
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

// --- Favorites API ---

export function getFavoriteRecipes(signal?: AbortSignal): Promise<FavoriteRecipeDto[]> {
  return authFetchApi<FavoriteRecipeDto[]>("/api/favorites/recipes", { signal });
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

export function getFavoriteStatus(recipeId: number): Promise<FavoriteStatusDto> {
  return authFetchApi<FavoriteStatusDto>(`/api/favorites/recipes/${recipeId}/status`);
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
  return authFetchApi(`/api/gear/recommendations/compare?${params}`, { signal });
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

export function saveCraftingPlan(req: {
  name: string;
  items: { recipeId: number; quantity: number }[];
}): Promise<SavedListDto> {
  return authFetchApi<SavedListDto>("/api/saved-lists/crafting-plan", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  });
}
