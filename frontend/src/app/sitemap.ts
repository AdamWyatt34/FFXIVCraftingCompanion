import type { MetadataRoute } from "next";
import { getManifestServer, listRecipesServer } from "@/lib/static-data/server";
import { recipePath } from "@/lib/routes";
import { getSiteUrl } from "@/lib/site-url";

const BASE_URL = getSiteUrl();

// Sitemap 0 is the static pages; the rest list recipe pages in chunks of RECIPES_PER_SITEMAP.
const RECIPES_PER_SITEMAP = 5000;

/** How many sitemap files exist: the static one plus one per chunk of recipes. */
export async function sitemapCount(): Promise<number> {
  const recipes = await listRecipesServer();
  return Math.ceil(recipes.length / RECIPES_PER_SITEMAP) + 1;
}

export async function generateSitemaps(): Promise<{ id: number }[]> {
  const chunks = await sitemapCount();
  return Array.from({ length: chunks }, (_, id) => ({ id }));
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

  const [recipes, manifest] = await Promise.all([listRecipesServer(), getManifestServer()]);
  const lastModified = manifest ? new Date(manifest.exportedAt) : new Date();
  const start = (sitemapId - 1) * RECIPES_PER_SITEMAP;
  return recipes.slice(start, start + RECIPES_PER_SITEMAP).map((recipe) => ({
    url: `${BASE_URL}${recipePath(recipe.recipeId, recipe.itemName)}`,
    lastModified,
    changeFrequency: "monthly",
    priority: 0.6,
  }));
}
