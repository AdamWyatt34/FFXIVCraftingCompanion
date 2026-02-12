"use client";

import { ArrowLeft } from "lucide-react";
import { ItemIcon } from "@/components/ui/ItemIcon";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import { CONDITION_COLORS } from "@/lib/action-icons";
import type { RecipeDto, RecipeSummaryDto, CraftType } from "@/types/api";
import type { CraftingState } from "@/types/simulator";

interface SimulatorHeaderProps {
  recipe: RecipeSummaryDto;
  fullRecipe?: RecipeDto;
  state: CraftingState;
  onBackToSetup: () => void;
}

export function SimulatorHeader({
  recipe,
  fullRecipe,
  state,
  onBackToSetup,
}: SimulatorHeaderProps) {
  const craftInfo = CRAFT_TYPE_INFO[recipe.craftType as CraftType];
  const conditionDisplay =
    state.condition === "GoodOmen" ? "Good Omen" : state.condition;

  return (
    <div className="sticky top-0 z-10 bg-bg-primary/95 backdrop-blur-sm pb-2 sm:pb-3 -mx-4 px-4 pt-1 border-b border-border">
      {/* Single row on mobile, two rows on sm+ */}
      <div className="flex items-center gap-2 sm:gap-3">
        <button
          onClick={onBackToSetup}
          className="text-text-secondary hover:text-gold transition-colors shrink-0"
          aria-label="Back to setup"
        >
          <ArrowLeft size={18} />
        </button>
        {fullRecipe ? (
          <div className="hidden sm:block">
            <ItemIcon
              iconUrl={fullRecipe.resultItem.iconUrl}
              alt={fullRecipe.resultItem.name}
              size="md"
            />
          </div>
        ) : null}
        <div className="min-w-0 flex-1">
          <h1 className="text-sm sm:text-base font-bold text-text-primary truncate">
            {fullRecipe?.resultItem.name ?? `Recipe Lv.${recipe.recipeLevel}`}
          </h1>
          <div className="hidden sm:flex items-center gap-2 text-xs text-text-muted">
            {craftInfo && (
              <span className={craftInfo.colorClass}>{craftInfo.name}</span>
            )}
            <span>Lv. {recipe.recipeLevel}</span>
          </div>
        </div>

        {/* Inline step/condition on mobile */}
        <div className="flex items-center gap-1.5 sm:hidden text-xs shrink-0">
          <span className="text-text-muted">#{state.step}</span>
          <span
            style={{
              color: CONDITION_COLORS[state.condition],
            }}
          >
            {conditionDisplay}
          </span>
          {state.innerQuietStacks > 0 && (
            <span className="text-teal-400">
              IQ{state.innerQuietStacks}
            </span>
          )}
        </div>
      </div>

      {/* Row 2: Step + Condition + IQ (sm+ only) */}
      <div className="hidden sm:flex items-center gap-3 text-sm mt-2 ml-8">
        <span className="text-text-secondary">Step {state.step}</span>
        <span className="text-text-muted">&#xB7;</span>
        <span
          style={{
            color: CONDITION_COLORS[state.condition],
            transition: "color 0.3s ease",
          }}
        >
          {conditionDisplay}
        </span>
        {state.innerQuietStacks > 0 && (
          <>
            <span className="text-text-muted">&#xB7;</span>
            <span className="text-teal-400">
              IQ &#xD7;{state.innerQuietStacks}
            </span>
          </>
        )}
      </div>
    </div>
  );
}
