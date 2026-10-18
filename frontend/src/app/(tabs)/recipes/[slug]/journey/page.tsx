import type { Metadata } from "next";
import { notFound, permanentRedirect } from "next/navigation";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import { recipeJourneyPath } from "@/lib/routes";
import type { CraftType } from "@/types/api";
import { resolveRecipeRoute } from "../resolve";
import JourneyPageClient from "./JourneyPageClient";

interface Props {
  params: Promise<{ slug: string }>;
}

export const dynamicParams = true;
export const revalidate = false;

export function generateStaticParams(): { slug: string }[] {
  return [];
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const route = await resolveRecipeRoute(slug);
  if (route.status !== "ok") return { title: "Recipe Not Found", robots: { index: false, follow: false } };

  const { recipe, craftingParameters } = route.data;
  const name = recipe.resultItem.name;
  const job = CRAFT_TYPE_INFO[recipe.craftType as CraftType]?.fullName ?? recipe.craftType;
  const title = `${name} Crafting Journey – FFXIV ${job} Lv. ${craftingParameters.classJobLevel}`;
  const description = `Everything to craft ${name} in Final Fantasy XIV: whether your stats are enough, what gear to upgrade, where to gather the materials and what they cost.`;
  return {
    title,
    description,
    alternates: { canonical: recipeJourneyPath(recipe.recipeId, name) },
    openGraph: { title, description },
  };
}

// A real 404 for unknown recipes and a 308 from a bare id or a stale name to the canonical slug;
// the journey itself is computed in the browser from the cached export and the signed-in profile.
export default async function JourneyPage({ params }: Props) {
  const { slug } = await params;
  const route = await resolveRecipeRoute(slug);
  if (route.status === "not-found") notFound();
  if (route.status === "redirect") permanentRedirect(recipeJourneyPath(route.recipeId, route.itemName));
  return <JourneyPageClient recipeId={route.recipeId} />;
}
