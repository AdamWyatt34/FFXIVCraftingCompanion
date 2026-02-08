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

    return (await response.json()) as T;
  } finally {
    clearTimeout(timeout);
  }
}

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

// Gear API

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
