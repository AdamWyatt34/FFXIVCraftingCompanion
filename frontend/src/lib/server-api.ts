import "server-only";

import type { RecipeDto } from "@/types/api";

const INTERNAL_API_URL =
  process.env.INTERNAL_API_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  "http://localhost:5159";

export async function getRecipeServer(
  id: number,
): Promise<RecipeDto | null> {
  try {
    const res = await fetch(
      `${INTERNAL_API_URL}/api/recipes/${id}?includeMaterials=true`,
      { next: { revalidate: 300 } },
    );

    if (!res.ok) return null;
    return (await res.json()) as RecipeDto;
  } catch {
    return null;
  }
}
