import type { Metadata } from "next";
import { listRecipesServer } from "@/lib/static-data/server";
import { countByJob } from "@/lib/static-data/hubs";
import { RecipeBrowse } from "@/components/recipes/RecipeBrowse";
import RecipesPageClient from "./RecipesPageClient";

// The search itself needs the query string, so it hydrates on the client behind a Suspense
// boundary and is not in the HTML. Everything a crawler should see (the heading, the job hubs and
// the quick filters) renders here, outside that boundary, from the exported recipe list.
export const metadata: Metadata = {
  title: "FFXIV Recipe Browser",
  description:
    "Search every Final Fantasy XIV crafting recipe by name, job and level. Ingredient trees, shopping lists and crafting order for all eight Disciples of the Hand.",
  alternates: { canonical: "/recipes" },
  openGraph: {
    title: "FFXIV Recipe Browser",
    description:
      "Search every Final Fantasy XIV crafting recipe by name, job and level. Ingredient trees, shopping lists and crafting order for all eight Disciples of the Hand.",
  },
};

export default async function Page() {
  const recipes = await listRecipesServer();
  return (
    <div className="flex flex-col h-full">
      <header className="px-4 pt-4">
        <h1 className="text-xl font-bold text-text-primary">FFXIV Crafting Recipes</h1>
        <p className="text-sm text-text-secondary mt-1">
          Search {recipes.length.toLocaleString("en-US")} recipes by item name, or browse by job and level.
        </p>
      </header>
      <RecipesPageClient />
      <RecipeBrowse counts={countByJob(recipes)} />
    </div>
  );
}
