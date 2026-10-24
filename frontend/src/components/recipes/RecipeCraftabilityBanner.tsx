"use client";

import Link from "next/link";
import { CheckCircle, AlertTriangle, Info, XCircle } from "lucide-react";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import type { CraftingParameters } from "@/lib/static-data/recipes";
import type { RecipeDto, CraftType } from "@/types/api";
import type { CharacterProfileDto } from "@/types/profile";

/** The recipe fields the banner compares against the profile; recipeLevel is the class job level. */
export type CraftabilityRecipe = Pick<RecipeDto, "craftType" | "recipeLevel"> &
  Pick<
    CraftingParameters,
    "requiredCraftsmanship" | "requiredControl" | "suggestedCraftsmanship" | "suggestedControl"
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

  const { craftsmanship, control } = profile.stats;
  // The game refuses to start the craft below these; below the recommended stats it only warns.
  const blocking: string[] = [];
  if (profile.activeJobLevel < recipe.recipeLevel) {
    blocking.push(
      `Your level (${profile.activeJobLevel}) is below the required ${recipe.recipeLevel}`,
    );
  }
  if (craftsmanship < recipe.requiredCraftsmanship) {
    blocking.push(
      `Your Craftsmanship (${craftsmanship}) is below the required ${recipe.requiredCraftsmanship}`,
    );
  }
  if (control < recipe.requiredControl) {
    blocking.push(
      `Your Control (${control}) is below the required ${recipe.requiredControl}`,
    );
  }

  const belowRecommended: string[] = [];
  if (craftsmanship < recipe.suggestedCraftsmanship) {
    belowRecommended.push(
      `Your Craftsmanship (${craftsmanship}) is below the recommended ${recipe.suggestedCraftsmanship}`,
    );
  }
  if (control < recipe.suggestedControl) {
    belowRecommended.push(
      `Your Control (${control}) is below the recommended ${recipe.suggestedControl}`,
    );
  }

  if (blocking.length === 0 && belowRecommended.length === 0) {
    return (
      <div className="flex items-center gap-2 px-4 py-2.5 rounded-lg bg-emerald-500/10 border border-emerald-500/20">
        <CheckCircle size={16} className="text-emerald-400 shrink-0" />
        <span className="text-sm text-emerald-300">
          You can craft this with your {craftInfo?.fullName ?? recipe.craftType}!
        </span>
      </div>
    );
  }

  const blocked = blocking.length > 0;
  const issues = blocked ? blocking : belowRecommended;
  const Icon = blocked ? XCircle : AlertTriangle;

  return (
    <div
      className={`flex items-start gap-2 px-4 py-2.5 rounded-lg border ${
        blocked ? "bg-red-500/10 border-red-500/20" : "bg-amber-500/10 border-amber-500/20"
      }`}
    >
      <Icon size={16} className={`${blocked ? "text-red-400" : "text-amber-400"} shrink-0 mt-0.5`} />
      <div className="space-y-1">
        {issues.map((issue) => (
          <p key={issue} className={`text-sm ${blocked ? "text-red-300" : "text-amber-300"}`}>
            {issue}
          </p>
        ))}
        <Link
          href={`/gear?job=${encodeURIComponent(profile.activeJob)}&level=${profile.activeJobLevel}&profile=balanced`}
          className={`inline-block text-sm underline underline-offset-2 transition-colors ${
            blocked ? "text-red-400 hover:text-red-300" : "text-amber-400 hover:text-amber-300"
          }`}
        >
          View gear upgrades
        </Link>
      </div>
    </div>
  );
}
