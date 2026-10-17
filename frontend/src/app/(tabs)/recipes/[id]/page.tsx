import type { Metadata } from "next";
import { getRecipeExportServer, listRecipeIdsServer } from "@/lib/static-data/server";
import RecipeDetailClient from "./RecipeDetailClient";

interface Props {
  params: Promise<{ id: string }>;
}

// Every recipe page is prerendered from the exported data at build time; ids outside the data
// set are 404s rather than server renders, so the API is never reached for a recipe.
export const dynamicParams = false;

export async function generateStaticParams(): Promise<{ id: string }[]> {
  const ids = await listRecipeIdsServer();
  return ids.map((id) => ({ id: String(id) }));
}

async function getRecipeServer(id: number) {
  return (await getRecipeExportServer(id))?.recipe ?? null;
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { id } = await params;
  const recipe = await getRecipeServer(Number(id));

  if (!recipe) {
    return { title: "Recipe Not Found" };
  }

  const title = `Craft ${recipe.resultItem.name} — Lv. ${recipe.recipeLevel}`;
  const description = `How to craft ${recipe.resultItem.name} in FFXIV. ${recipe.ingredients.length} ingredients, ${recipe.difficulty} difficulty, ${recipe.durability} durability. View shopping list, material tree, and crafting steps.`;

  return {
    title,
    description,
    openGraph: {
      title,
      description,
      images: recipe.resultItem.iconUrl
        ? [{ url: recipe.resultItem.iconUrl, width: 48, height: 48 }]
        : undefined,
    },
    twitter: {
      card: "summary",
      title,
      description,
    },
  };
}

export default async function RecipeDetailPage({ params }: Props) {
  const { id } = await params;
  const recipe = await getRecipeServer(Number(id));

  return (
    <>
      {recipe && (
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{
            __html: JSON.stringify({
              "@context": "https://schema.org",
              "@type": "HowTo",
              name: `Craft ${recipe.resultItem.name}`,
              description: `FFXIV crafting recipe for ${recipe.resultItem.name} (Lv. ${recipe.recipeLevel})`,
              totalTime: `PT${Math.ceil(recipe.durability / 10)}M`,
              supply: recipe.ingredients.map((ing) => ({
                "@type": "HowToSupply",
                name: ing.itemName,
                requiredQuantity: ing.quantity,
              })),
              step: [
                {
                  "@type": "HowToStep",
                  name: "Gather materials",
                  text: `Collect ${recipe.ingredients.length} ingredients from the shopping list.`,
                },
                {
                  "@type": "HowToStep",
                  name: "Craft the item",
                  text: `Use a ${recipe.craftType} class at level ${recipe.recipeLevel} or higher.`,
                },
              ],
            }),
          }}
        />
      )}
      <RecipeDetailClient />
    </>
  );
}
