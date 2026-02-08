import type {
  ItemDto,
  ItemSearchResultDto,
  RecipeDto,
  MaterialTreeResultDto,
  ShoppingListResultDto,
  ApiError,
} from "@/types/api";

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
