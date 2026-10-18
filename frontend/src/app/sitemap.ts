import type { MetadataRoute } from "next";
import { getManifestServer, listRecipeIdsServer } from "@/lib/static-data/server";
import { recipePath } from "@/lib/routes";

const BASE_URL =
  process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000";

// Sitemap 0 is the static pages; the rest list recipe pages in chunks of RECIPES_PER_SITEMAP.
const RECIPES_PER_SITEMAP = 5000;

export async function generateSitemaps(): Promise<{ id: number }[]> {
  const recipeIds = await listRecipeIdsServer();
  const chunks = Math.ceil(recipeIds.length / RECIPES_PER_SITEMAP);
  return Array.from({ length: chunks + 1 }, (_, id) => ({ id }));
}

export default async function sitemap({ id }: { id: number | string | Promise<number | string> }): Promise<MetadataRoute.Sitemap> {
  // Next hands the id over as a number, a string or a promise of either depending on version.
  const sitemapId = Number(await id);
  if (sitemapId === 0) {
    return [
      {
        url: BASE_URL,
        lastModified: new Date(),
        changeFrequency: "monthly",
        priority: 1.0,
      },
      {
        url: `${BASE_URL}/recipes`,
        lastModified: new Date(),
        changeFrequency: "daily",
        priority: 0.9,
      },
      {
        url: `${BASE_URL}/gear`,
        lastModified: new Date(),
        changeFrequency: "weekly",
        priority: 0.7,
      },
      {
        url: `${BASE_URL}/simulator`,
        lastModified: new Date(),
        changeFrequency: "weekly",
        priority: 0.7,
      },
    ];
  }

  const [recipeIds, manifest] = await Promise.all([listRecipeIdsServer(), getManifestServer()]);
  const lastModified = manifest ? new Date(manifest.exportedAt) : new Date();
  const start = (sitemapId - 1) * RECIPES_PER_SITEMAP;
  return recipeIds.slice(start, start + RECIPES_PER_SITEMAP).map((recipeId) => ({
    url: `${BASE_URL}${recipePath(recipeId)}`,
    lastModified,
    changeFrequency: "monthly",
    priority: 0.6,
  }));
}
