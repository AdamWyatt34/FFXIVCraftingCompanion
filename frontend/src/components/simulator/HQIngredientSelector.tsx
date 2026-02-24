"use client";

import { useState, useCallback, useMemo } from "react";
import { Minus, Plus, Star } from "lucide-react";
import Image from "next/image";
import type { RecipeDto, RecipeIngredientDto } from "@/types/api";
import type { HQIngredientInput } from "@/types/simulator";

interface HQIngredientSelectorProps {
  recipe: RecipeDto;
  onChange: (hqIngredients: HQIngredientInput[]) => void;
}

export function HQIngredientSelector({ recipe, onChange }: HQIngredientSelectorProps) {
  const [hqCounts, setHqCounts] = useState<Record<number, number>>({});

  const hqableIngredients = useMemo(
    () => recipe.ingredients.filter((i) => i.canBeHQ),
    [recipe.ingredients],
  );

  const nonHqIngredients = useMemo(
    () => recipe.ingredients.filter((i) => !i.canBeHQ),
    [recipe.ingredients],
  );

  const updateCount = useCallback(
    (itemId: number, quantity: number, delta: number) => {
      setHqCounts((prev) => {
        const current = prev[itemId] ?? 0;
        const next = Math.max(0, Math.min(quantity, current + delta));
        const updated = { ...prev, [itemId]: next };

        // Emit change
        const hqIngredients: HQIngredientInput[] = [];
        for (const [id, qty] of Object.entries(updated)) {
          if (qty > 0) {
            hqIngredients.push({ itemId: Number(id), hqQuantity: qty });
          }
        }
        onChange(hqIngredients);

        return updated;
      });
    },
    [onChange],
  );

  const setAllHQ = useCallback(() => {
    const updated: Record<number, number> = {};
    const hqIngredients: HQIngredientInput[] = [];
    for (const ing of hqableIngredients) {
      updated[ing.itemId] = ing.quantity;
      hqIngredients.push({ itemId: ing.itemId, hqQuantity: ing.quantity });
    }
    setHqCounts(updated);
    onChange(hqIngredients);
  }, [hqableIngredients, onChange]);

  const setAllNQ = useCallback(() => {
    setHqCounts({});
    onChange([]);
  }, [onChange]);

  if (hqableIngredients.length === 0) {
    return null;
  }

  return (
    <div className="bg-bg-surface border border-border rounded-lg p-4">
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-sm font-semibold text-text-primary flex items-center gap-1.5">
          <Star size={14} className="text-gold" />
          HQ Ingredients
        </h3>
        <div className="flex gap-2">
          <button
            onClick={setAllNQ}
            className="px-2 py-0.5 text-xs text-text-muted hover:text-text-secondary border border-border rounded transition-colors"
          >
            All NQ
          </button>
          <button
            onClick={setAllHQ}
            className="px-2 py-0.5 text-xs text-gold hover:text-gold/80 border border-gold/30 rounded transition-colors"
          >
            All HQ
          </button>
        </div>
      </div>

      <div className="space-y-2">
        {hqableIngredients.map((ingredient) => (
          <IngredientRow
            key={ingredient.itemId}
            ingredient={ingredient}
            hqCount={hqCounts[ingredient.itemId] ?? 0}
            onIncrement={() => updateCount(ingredient.itemId, ingredient.quantity, 1)}
            onDecrement={() => updateCount(ingredient.itemId, ingredient.quantity, -1)}
          />
        ))}

        {nonHqIngredients.length > 0 && (
          <div className="pt-2 border-t border-border/50">
            {nonHqIngredients.map((ingredient) => (
              <div
                key={ingredient.itemId}
                className="flex items-center gap-2 py-1 opacity-50"
              >
                {ingredient.iconUrl && (
                  <Image
                    src={ingredient.iconUrl}
                    alt=""
                    width={20}
                    height={20}
                    className="rounded"
                  />
                )}
                <span className="text-xs text-text-muted flex-1 truncate">
                  {ingredient.itemName}
                </span>
                <span className="text-xs text-text-muted italic">Cannot be HQ</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function IngredientRow({
  ingredient,
  hqCount,
  onIncrement,
  onDecrement,
}: {
  ingredient: RecipeIngredientDto;
  hqCount: number;
  onIncrement: () => void;
  onDecrement: () => void;
}) {
  const nqCount = ingredient.quantity - hqCount;

  return (
    <div className="flex items-center gap-2">
      {ingredient.iconUrl && (
        <Image
          src={ingredient.iconUrl}
          alt=""
          width={24}
          height={24}
          className="rounded"
        />
      )}
      <span className="text-sm text-text-primary flex-1 truncate min-w-0">
        {ingredient.itemName}
      </span>

      {/* NQ count */}
      <span className="text-xs text-text-muted w-8 text-right">
        {nqCount} NQ
      </span>

      {/* HQ counter */}
      <div className="flex items-center gap-1">
        <button
          onClick={onDecrement}
          disabled={hqCount === 0}
          className="w-6 h-6 flex items-center justify-center rounded bg-bg-elevated text-text-muted hover:text-text-primary disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
          aria-label={`Decrease HQ count for ${ingredient.itemName}`}
        >
          <Minus size={12} />
        </button>

        <span
          className={`w-6 text-center text-sm font-mono ${
            hqCount > 0 ? "text-gold font-bold" : "text-text-muted"
          }`}
        >
          {hqCount}
        </span>

        <button
          onClick={onIncrement}
          disabled={hqCount >= ingredient.quantity}
          className="w-6 h-6 flex items-center justify-center rounded bg-bg-elevated text-text-muted hover:text-gold disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
          aria-label={`Increase HQ count for ${ingredient.itemName}`}
        >
          <Plus size={12} />
        </button>
      </div>

      <span className="text-xs text-gold w-6 text-right">
        {hqCount > 0 && `${hqCount}`}
        {hqCount > 0 && <Star size={8} className="inline ml-0.5 -mt-0.5" />}
      </span>
    </div>
  );
}
