import { ApiClientError } from "@/lib/api-errors";
import * as staticData from "@/lib/static-data/client";
import type {
  ItemDto,
  ItemSearchResultDto,
  RecipeDto,
  RecipeCraftingParametersDto,
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
import type {
  SubmitRotationRequest,
  RotationDetail,
  RotationListResult,
  RotationSearchParams,
  UpvoteResult,
} from "@/types/solver";

export { ApiClientError };

function getBaseUrl(): string {
  if (typeof window !== "undefined" && process.env.NODE_ENV === "development") {
    return "";
  }
  return process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5159";
}

// In-flight GET request deduplication
const inflightGets = new Map<string, Promise<unknown>>();

async function fetchApi<T>(path: string, init?: RequestInit): Promise<T> {
  const method = init?.method?.toUpperCase() ?? "GET";
  // Only deduplicate GETs without a caller-provided abort signal.
  // Requests with signals manage their own lifecycle (e.g. hooks with
  // AbortControllers) and must not receive a promise that another
  // controller may abort — otherwise React Strict Mode double-mount
  // causes a permanent loading state.
  if (method === "GET" && !init?.signal) {
    const existing = inflightGets.get(path);
    if (existing) return existing as Promise<T>;

    const promise = fetchApiInternal<T>(path, init).finally(() => {
      inflightGets.delete(path);
    });
    inflightGets.set(path, promise);
    return promise;
  }
  return fetchApiInternal<T>(path, init);
}

async function fetchApiInternal<T>(
  path: string,
  init?: RequestInit,
): Promise<T> {
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

// Game data is read from the exported static files (src/lib/static-data), not the API. The
// signatures and the DTOs are the API's, so callers do not care which one answered.

export function searchItems(
  query: string | null,
  page = 1,
  pageSize = 20,
  signal?: AbortSignal,
  craftType?: string,
): Promise<ItemSearchResultDto> {
  return staticData.searchItems(query, page, pageSize, signal, craftType);
}

export function getItem(id: number): Promise<ItemDto> {
  return staticData.getItem(id);
}

// --- Recipes API ---

export function getRecipe(
  id: number,
  includeMaterials = false,
): Promise<RecipeDto> {
  return staticData.getRecipe(id, includeMaterials);
}

export function getRecipeCraftingParameters(
  id: number,
  signal?: AbortSignal,
): Promise<RecipeCraftingParametersDto> {
  return staticData.getRecipeCraftingParameters(id, signal);
}

export function getMaterialTree(
  recipeId: number,
  quantity = 1,
  signal?: AbortSignal,
): Promise<MaterialTreeResultDto> {
  return staticData.getMaterialTree(recipeId, quantity, signal);
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
  craftedOnly?: boolean,
): Promise<GearSetDto> {
  return staticData.getGearRecommendations(job, level, profile, signal, craftedOnly ?? false);
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
  return fetchApi<GearSlotDto>(`/api/gear/recommendations/${slot}?${params}`, {
    signal,
  });
}

export function getStatWeightProfiles(
  job: string,
  signal?: AbortSignal,
): Promise<StatWeightProfileDto[]> {
  return staticData.getStatWeightProfiles(job, signal);
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
  return staticData.searchEquipment(query, job, slot, minLevel, maxLevel, signal);
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

export function authLogout(
  accessToken: string,
  refreshToken: string,
): Promise<void> {
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
  return fetchApi<LodestonePreviewDto>(
    `/api/profile/lodestone-preview?${params}`,
    { signal },
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

export function getFavoriteStatus(
  recipeId: number,
): Promise<FavoriteStatusDto> {
  return authFetchApi<FavoriteStatusDto>(
    `/api/favorites/recipes/${recipeId}/status`,
  );
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

// --- Journey API ---

/** The journey; with a viewer (a signed-in profile and the engine) it carries the stat check. */
export function getCraftingJourney(
  recipeId: number,
  signal?: AbortSignal,
  viewer: import("@/lib/static-data/sufficiency").JourneyViewer | null = null,
): Promise<import("@/types/journey").CraftingJourneyDto> {
  return staticData.getCraftingJourney(recipeId, signal, viewer);
}

export function getItemContext(
  itemId: number,
  signal?: AbortSignal,
): Promise<import("@/types/journey").ItemContextDto> {
  return staticData.getItemContext(itemId, signal);
}

// --- Market API ---

export function getMarketPrices(
  world: string,
  itemIds: number[],
  signal?: AbortSignal,
): Promise<Record<number, import("@/types/market").MarketBoardDataDto>> {
  const params = new URLSearchParams({
    world,
    items: itemIds.join(","),
  });
  return fetchApi(`/api/market/prices?${params}`, { signal });
}

export function getWorldList(): Promise<import("@/types/market").WorldGroupDto[]> {
  return staticData.getWorldList();
}

export function getRecipeCost(
  recipeId: number,
  world: string,
  quantity = 1,
  options?: { useMinPrice?: boolean; includeGatheredItemPrices?: boolean },
  signal?: AbortSignal,
): Promise<import("@/types/market").CostEstimateDto> {
  return staticData.getRecipeCost(recipeId, world, quantity, options ?? {}, signal);
}

// --- Gathering API ---

export function getGatheringGuide(
  items: import("@/types/gathering").GatheringGuideItemInput[],
  signal?: AbortSignal,
): Promise<import("@/types/gathering").GatheringGuideDto> {
  return staticData.getGatheringGuide(items, signal);
}

export function getEorzeanTime(): Promise<import("@/types/gathering").EorzeanTimeResponse> {
  return staticData.getEorzeanTime();
}

// --- Craft Plans API ---

export function resolveCraftPlan(
  items: import("@/types/craft-plan").CraftPlanItemInput[],
  signal?: AbortSignal,
): Promise<import("@/types/craft-plan").CraftPlanResultDto> {
  return staticData.resolveCraftPlan(items, signal);
}

export function exportCraftPlan(
  items: import("@/types/craft-plan").CraftPlanItemInput[],
  format: string,
  signal?: AbortSignal,
): Promise<import("@/types/craft-plan").ExportResultDto> {
  return staticData.exportCraftPlan(items, format, signal);
}

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

export async function getResolvedCraftPlan(
  id: string,
  signal?: AbortSignal,
): Promise<import("@/types/craft-plan").CraftPlanResultDto> {
  const plan = await getSharedCraftPlan(id, signal);
  if (plan.items.length === 0)
    throw new ApiClientError(404, { error: "Craft plan not found." });
  return staticData.resolveCraftPlan(plan.items, signal);
}

export function updateCraftPlan(
  id: string,
  request: import("@/types/craft-plan").UpdateCraftPlanRequest,
): Promise<import("@/types/craft-plan").CraftPlanDto> {
  return authFetchApi(`/api/craft-plans/${encodeURIComponent(id)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
}

export function deleteCraftPlan(id: string): Promise<void> {
  return authFetchApi(`/api/craft-plans/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
}

export function getCraftPlanCost(
  id: string,
  world: string,
  signal?: AbortSignal,
): Promise<import("@/types/market").CostEstimateDto> {
  const params = new URLSearchParams({ world });
  return fetchApi(`/api/craft-plans/${encodeURIComponent(id)}/cost?${params}`, {
    method: "POST",
    signal,
  });
}

// --- BiS Stats (from gear recommendations) ---

export async function getBisStats(
  job: string,
  level: number,
  signal?: AbortSignal,
): Promise<{ craftsmanship: number; control: number; cp: number }> {
  const gearSet = await getGearRecommendations(
    job,
    level,
    "balanced",
    signal,
    false,
  );
  return {
    craftsmanship: gearSet.totalStats.craftsmanship,
    control: gearSet.totalStats.control,
    cp: gearSet.totalStats.cp,
  };
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
