"use client";

import { useState, useMemo } from "react";
import Link from "next/link";
import {
  Check,
  ChevronDown,
  ChevronRight,
  ExternalLink,
} from "lucide-react";
import { ItemIcon, ItemLink, SourceBadge } from "@/components/ui";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import { buildIngredientMap } from "@/lib/tree-utils";
import { recipePath } from "@/lib/routes";
import type {
  IntermediateCraftDto,
  MaterialTreeNodeDto,
  CraftType,
} from "@/types/api";

interface CraftingStepsViewProps {
  intermediateCrafts: IntermediateCraftDto[];
  tree: MaterialTreeNodeDto;
}

interface CraftStep {
  key: string;
  itemId: number;
  itemName: string;
  iconUrl: string;
  craftType: string;
  craftTypeName: string;
  recipeId: number;
  quantity: number;
  recipeLevel: number;
  stepNumber: number;
  isFinal: boolean;
}

export function CraftingStepsView({
  intermediateCrafts,
  tree,
}: CraftingStepsViewProps) {
  const ingredientMap = useMemo(() => buildIngredientMap(tree), [tree]);

  const steps: CraftStep[] = useMemo(() => {
    const sorted = [...intermediateCrafts].sort(
      (a, b) => a.craftOrder - b.craftOrder,
    );
    const mapped: CraftStep[] = sorted.map((craft, i) => ({
      key: `intermediate-${craft.recipeId}`,
      itemId: craft.itemId,
      itemName: craft.itemName,
      iconUrl: craft.iconUrl,
      craftType: craft.craftType,
      craftTypeName: craft.craftTypeName,
      recipeId: craft.recipeId,
      quantity: craft.quantity,
      recipeLevel: craft.recipeLevel,
      stepNumber: i + 1,
      isFinal: false,
    }));

    // Add final item as last step
    if (tree.recipe) {
      mapped.push({
        key: `final-${tree.recipe.recipeId}`,
        itemId: tree.itemId,
        itemName: tree.itemName,
        iconUrl: tree.iconUrl,
        craftType: tree.recipe.craftType,
        craftTypeName: tree.recipe.craftTypeName,
        recipeId: tree.recipe.recipeId,
        quantity: tree.quantity,
        recipeLevel: tree.recipe.recipeLevel,
        stepNumber: mapped.length + 1,
        isFinal: true,
      });
    }

    return mapped;
  }, [intermediateCrafts, tree]);

  const [checkedSteps, setCheckedSteps] = useState<Set<string>>(new Set());
  const [expandedStep, setExpandedStep] = useState<string | null>(null);

  const firstUnchecked = steps.find((s) => !checkedSteps.has(s.key))?.key;

  function toggleChecked(key: string) {
    setCheckedSteps((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  function toggleExpanded(key: string) {
    setExpandedStep((prev) => (prev === key ? null : key));
  }

  if (steps.length === 0) {
    return (
      <div className="text-center text-text-muted py-8 text-sm">
        No intermediate crafts needed. All ingredients are raw materials.
      </div>
    );
  }

  return (
    <div className="space-y-0">
      {steps.map((step, index) => {
        const isChecked = checkedSteps.has(step.key);
        const isCurrent = step.key === firstUnchecked;
        const isExpanded = expandedStep === step.key;
        const ingredients = ingredientMap.get(step.itemId) ?? [];
        const craftInfo =
          CRAFT_TYPE_INFO[step.craftType as CraftType] ?? null;
        const isLast = index === steps.length - 1;

        return (
          <div key={step.key} className="flex gap-3">
            {/* Timeline column */}
            <div className="flex flex-col items-center shrink-0 w-10">
              {/* Checkbox / step number */}
              <button
                onClick={() => toggleChecked(step.key)}
                className={`w-10 h-10 rounded-full flex items-center justify-center shrink-0 transition-colors ${
                  isChecked
                    ? "bg-emerald-500/20 text-emerald-400"
                    : isCurrent
                      ? "bg-gold/20 text-gold ring-1 ring-gold"
                      : "bg-bg-elevated text-text-muted"
                }`}
                aria-label={
                  isChecked
                    ? `Mark step ${step.stepNumber} incomplete`
                    : `Mark step ${step.stepNumber} complete`
                }
              >
                {isChecked ? (
                  <Check size={18} />
                ) : (
                  <span className="text-xs font-bold">{step.stepNumber}</span>
                )}
              </button>
              {/* Timeline line */}
              {!isLast && (
                <div
                  className={`w-0.5 flex-1 mt-1 ${
                    isChecked ? "bg-emerald-500/30" : "bg-border"
                  }`}
                />
              )}
            </div>

            {/* Step content */}
            <div
              className={`flex-1 mb-3 rounded-lg border transition-all ${
                isChecked
                  ? "bg-bg-surface/50 border-border opacity-60"
                  : isCurrent
                    ? "bg-bg-surface border-gold/40"
                    : "bg-bg-surface border-border"
              }`}
            >
              {/* Step header - clickable to expand */}
              <button
                onClick={() => toggleExpanded(step.key)}
                className="w-full flex items-center gap-3 p-3 text-left min-h-[44px]"
              >
                <ItemIcon
                  iconUrl={step.iconUrl}
                  alt=""
                  size="md"
                />
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span
                      className={`text-sm font-medium ${
                        isChecked
                          ? "text-text-muted line-through"
                          : "text-text-primary"
                      }`}
                    >
                      {step.itemName}
                    </span>
                    {step.isFinal && (
                      <span className="px-1.5 py-0.5 rounded text-[10px] font-bold uppercase bg-gold/20 text-gold">
                        Final
                      </span>
                    )}
                  </div>
                  <div className="flex items-center gap-2 mt-0.5 flex-wrap">
                    {craftInfo && (
                      <span
                        className={`text-xs ${craftInfo.colorClass}`}
                      >
                        {craftInfo.fullName}
                      </span>
                    )}
                    <span className="text-xs text-text-muted">
                      Lv. {step.recipeLevel}
                    </span>
                  </div>
                </div>
                <div className="flex items-center gap-2 shrink-0">
                  <span className="text-sm font-bold text-text-primary font-mono">
                    ×{step.quantity}
                  </span>
                  {ingredients.length > 0 &&
                    (isExpanded ? (
                      <ChevronDown
                        size={14}
                        className="text-text-muted"
                      />
                    ) : (
                      <ChevronRight
                        size={14}
                        className="text-text-muted"
                      />
                    ))}
                </div>
              </button>

              {/* Expanded ingredients */}
              {isExpanded && ingredients.length > 0 && (
                <div className="border-t border-border/50 px-3 pb-3 pt-2">
                  <div className="text-xs text-text-muted uppercase tracking-wider mb-2">
                    Ingredients
                  </div>
                  <div className="space-y-1.5">
                    {ingredients.map((ing, i) => (
                      <div
                        key={`${ing.itemId}-${i}`}
                        className="flex items-center gap-2"
                      >
                        <ItemLink
                          itemId={ing.itemId}
                          itemName={ing.itemName}
                          iconUrl={ing.iconUrl}
                          compact
                          className="flex-1 min-w-0"
                        />
                        <SourceBadge
                          sourceType={ing.primarySource}
                          className="shrink-0"
                        />
                        <span className="text-xs text-text-muted font-mono shrink-0">
                          ×{ing.quantity}
                        </span>
                      </div>
                    ))}
                  </div>
                  <Link
                    href={recipePath(step.recipeId, step.itemName)}
                    className="inline-flex items-center gap-1 mt-2 text-xs text-gold hover:text-gold-light transition-colors"
                  >
                    View Recipe
                    <ExternalLink size={12} />
                  </Link>
                </div>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
