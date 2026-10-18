import { ImageResponse } from "next/og";
import { getRecipeExportServer } from "@/lib/static-data/server";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import type { CraftType } from "@/types/api";

// Text-only card rendered on demand (only when a social bot fetches it); no remote icon fetch.
export const alt = "FFXIV recipe";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

export default async function RecipeOpenGraphImage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const data = /^\d{1,9}$/.test(id) ? await getRecipeExportServer(Number(id)) : null;
  const name = data?.recipe.resultItem.name ?? "Recipe";
  const job = data ? (CRAFT_TYPE_INFO[data.recipe.craftType as CraftType]?.fullName ?? data.recipe.craftType) : "";
  const level = data?.craftingParameters.classJobLevel;
  // The image font has no "★" glyph, so stars are spelled out.
  const stars = data && data.craftingParameters.stars > 0 ? ` (${data.craftingParameters.stars}-star)` : "";
  const ingredients = data?.recipe.ingredients.length ?? 0;

  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          padding: "72px 88px",
          background: "linear-gradient(135deg, #1a1a2e 0%, #16213e 60%, #0f3460 100%)",
          color: "#e0e0e0",
          fontFamily: "sans-serif",
        }}
      >
        <div style={{ display: "flex", fontSize: 28, letterSpacing: 6, color: "#e6b422" }}>
          FFXIV CRAFTING RECIPE
        </div>
        <div
          style={{
            display: "flex",
            fontSize: name.length > 32 ? 56 : 72,
            fontWeight: 700,
            marginTop: 18,
            color: "#ffffff",
            lineHeight: 1.15,
          }}
        >
          {name}
        </div>
        {data && (
          <div style={{ display: "flex", fontSize: 34, marginTop: 24, color: "#c8c8d8" }}>
            {job} · Lv. {level}{stars} · {ingredients} ingredient{ingredients === 1 ? "" : "s"}
          </div>
        )}
        <div style={{ display: "flex", fontSize: 26, marginTop: 48, color: "#e6b422" }}>
          Materials, shopping list, crafting order and a solver macro — ffxivcrafting.app
        </div>
      </div>
    ),
    size,
  );
}
