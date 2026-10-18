"use client";

import Link from "next/link";
import { CheckCircle, AlertTriangle, Info } from "lucide-react";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import type { RecipeDto, CraftType } from "@/types/api";
import type { CharacterProfileDto } from "@/types/profile";

/** The recipe fields the banner compares against the profile; recipeLevel is the class job level. */
export type CraftabilityRecipe = Pick<
  RecipeDto,
  "craftType" | "recipeLevel" | "requiredCraftsmanship" | "requiredControl"
>;

interface RecipeCraftabilityBannerProps {
  recipe: CraftabilityRecipe;
  profile: CharacterProfileDto;
}

export function RecipeCraftabilityBanner({
  recipe,
  profile,
}: RecipeCraftabilityBannerProps) {
  const craftInfo = CRAFT_TYPE_INFO[recipe.craftType as CraftType];
  const jobMatches = profile.activeJob === recipe.craftType;

  if (!jobMatches) {
    return (
      <div className="flex items-center gap-2 px-4 py-2.5 rounded-lg bg-bg-surface border border-border">
        <Info size={16} className="text-text-muted shrink-0" />
        <span className="text-sm text-text-secondary">
          This recipe requires {craftInfo?.fullName ?? recipe.craftType}
        </span>
      </div>
    );
  }

  const levelMet = profile.activeJobLevel >= recipe.recipeLevel;
  const craftsMet = profile.stats.craftsmanship >= recipe.requiredCraftsmanship;
  const controlMet = profile.stats.control >= recipe.requiredControl;
  const allStatsMet = levelMet && craftsMet && controlMet;

  if (allStatsMet) {
    return (
      <div className="flex items-center gap-2 px-4 py-2.5 rounded-lg bg-emerald-500/10 border border-emerald-500/20">
        <CheckCircle size={16} className="text-emerald-400 shrink-0" />
        <span className="text-sm text-emerald-300">
          You can craft this with your {craftInfo?.fullName ?? recipe.craftType}!
        </span>
      </div>
    );
  }

  // Job matches but stats are insufficient
  const issues: string[] = [];
  if (!levelMet) {
    issues.push(
      `Your level (${profile.activeJobLevel}) is below the required ${recipe.recipeLevel}`,
    );
  }
  if (!craftsMet) {
    issues.push(
      `Your Craftsmanship (${profile.stats.craftsmanship}) is below the required ${recipe.requiredCraftsmanship}`,
    );
  }
  if (!controlMet) {
    issues.push(
      `Your Control (${profile.stats.control}) is below the required ${recipe.requiredControl}`,
    );
  }

  return (
    <div className="flex items-start gap-2 px-4 py-2.5 rounded-lg bg-amber-500/10 border border-amber-500/20">
      <AlertTriangle size={16} className="text-amber-400 shrink-0 mt-0.5" />
      <div className="space-y-1">
        {issues.map((issue) => (
          <p key={issue} className="text-sm text-amber-300">
            {issue}
          </p>
        ))}
        <Link
          href={`/gear?job=${encodeURIComponent(profile.activeJob)}&level=${profile.activeJobLevel}&profile=balanced`}
          className="inline-block text-sm text-amber-400 hover:text-amber-300 underline underline-offset-2 transition-colors"
        >
          View gear upgrades
        </Link>
      </div>
    </div>
  );
}
