"use client";

import { useProfile } from "@/hooks/useProfile";
import { RecipeCraftabilityBanner, type CraftabilityRecipe } from "./RecipeCraftabilityBanner";

/** Shows the craftability banner once a signed-in profile is known; nothing for visitors. */
export function RecipeCraftability({ recipe }: { recipe: CraftabilityRecipe }) {
  const { profile } = useProfile();
  if (!profile) return null;
  return <RecipeCraftabilityBanner recipe={recipe} profile={profile} />;
}
