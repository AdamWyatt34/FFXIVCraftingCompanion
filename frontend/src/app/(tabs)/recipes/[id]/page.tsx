import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { getRecipeExportServer } from "@/lib/static-data/server";
import RecipeDetailClient from "./RecipeDetailClient";

interface Props {
  params: Promise<{ id: string }>;
}

// Rendered on first request from the exported data and cached for the deployment's lifetime.
// Prerendering all ~14k recipes measured 1.4 GB of build output (about 100 KB a page), too much
// to upload on every deploy for pages that are static anyway; the API is never involved.
export const dynamicParams = true;
export const revalidate = false;

export function generateStaticParams(): { id: string }[] {
  return [];
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
  if (!recipe) notFound();

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
