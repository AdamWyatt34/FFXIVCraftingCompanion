import { cache } from "react";
import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { getRecipeExportServer } from "@/lib/static-data/server";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import { recipeJourneyPath } from "@/lib/routes";
import type { CraftType } from "@/types/api";
import JourneyPageClient from "./JourneyPageClient";

interface Props {
  params: Promise<{ id: string }>;
}

export const dynamicParams = true;
export const revalidate = false;

export function generateStaticParams(): { id: string }[] {
  return [];
}

// One bucket read per request, shared by the metadata and the page.
const resolveRecipe = cache(async (idParam: string) =>
  /^\d{1,9}$/.test(idParam) ? getRecipeExportServer(Number(idParam)) : null,
);

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { id } = await params;
  const data = await resolveRecipe(id);
  if (!data) return { title: "Recipe Not Found", robots: { index: false, follow: false } };

  const name = data.recipe.resultItem.name;
  const job = CRAFT_TYPE_INFO[data.recipe.craftType as CraftType]?.fullName ?? data.recipe.craftType;
  const title = `${name} Crafting Journey – FFXIV ${job} Lv. ${data.craftingParameters.classJobLevel}`;
  const description = `Everything to craft ${name} in Final Fantasy XIV: whether your stats are enough, what gear to upgrade, where to gather the materials and what they cost.`;
  return {
    title,
    description,
    alternates: { canonical: recipeJourneyPath(data.recipe.recipeId, name) },
    openGraph: { title, description },
  };
}

// A real 404 for unknown recipes; the journey itself is computed in the browser from the
// cached export and the signed-in profile.
export default async function JourneyPage({ params }: Props) {
  const { id } = await params;
  if (!(await resolveRecipe(id))) notFound();
  return <JourneyPageClient />;
}
