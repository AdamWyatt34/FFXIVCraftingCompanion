"use client";

import { useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import {
  ArrowLeft,
  FlaskConical,
  Copy,
  ClipboardList,
} from "lucide-react";
import { useJourney } from "@/hooks/useJourney";
import { useBreadcrumbs } from "@/contexts/BreadcrumbContext";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import { ItemIcon, ErrorState } from "@/components/ui";
import {
  ShoppingListView,
} from "@/components/recipes";
import {
  StatCheck,
  GearUpgrades,
  CostEstimate,
  GatheringGuide,
  JourneySkeleton,
} from "@/components/journey";
import type { CraftType } from "@/types/api";

export default function JourneyPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const recipeId = Number(params.id);
  const { journey, isLoading, error, retry } = useJourney(recipeId);
  const { setBreadcrumbs } = useBreadcrumbs();

  useEffect(() => {
    if (journey) {
      const itemName = journey.itemContext.itemName;
      setBreadcrumbs([
        { label: "Recipes", href: "/recipes" },
        { label: itemName, href: `/recipes/${recipeId}` },
        { label: "Journey" },
      ]);
    }
  }, [journey, recipeId, setBreadcrumbs]);

  if (isNaN(recipeId)) {
    return (
      <div className="p-4">
        <ErrorState message="Invalid recipe ID" />
      </div>
    );
  }

  if (isLoading) return <JourneySkeleton />;

  if (error || !journey) {
    return (
      <div className="p-4">
        <ErrorState message={error ?? "Journey not found"} onRetry={retry} />
      </div>
    );
  }

  const ctx = journey.itemContext;
  const recipe = ctx.recipes[0];
  const craftInfo = recipe ? CRAFT_TYPE_INFO[recipe.craftType as CraftType] : null;
  const materials = journey.materials;

  const handleCopyShoppingList = () => {
    const lines = materials.shoppingList
      .map((i) => `${i.itemName} x${i.totalQuantity}`)
      .join("\n");
    const crystals = materials.crystalRequirements
      .map((c) => `${c.itemName} x${c.totalQuantity}`)
      .join("\n");
    const text = `Shopping List for ${ctx.itemName}\n${"=".repeat(30)}\n${crystals}\n\n${lines}`;
    navigator.clipboard.writeText(text).catch(() => {});
  };

  return (
    <div className="p-4 space-y-6 max-w-4xl mx-auto">
      {/* Back navigation + breadcrumbs */}
      <div className="flex items-center gap-2 text-xs text-text-muted">
        <button
          onClick={() => router.back()}
          className="flex items-center gap-1 text-text-secondary hover:text-gold transition-colors text-sm"
        >
          <ArrowLeft size={16} />
        </button>
        <Link href="/recipes" className="hover:text-gold transition-colors">Recipes</Link>
        <span>&gt;</span>
        <Link href={`/recipes/${recipeId}`} className="hover:text-gold transition-colors truncate">
          {ctx.itemName}
        </Link>
        <span>&gt;</span>
        <span className="text-text-secondary">Journey</span>
      </div>

      {/* Sticky header */}
      <div className="flex items-start gap-4">
        <ItemIcon iconUrl={ctx.iconUrl} alt={ctx.itemName} size="2xl" />
        <div className="flex-1 min-w-0">
          <h1 className="text-xl font-bold text-text-primary">{ctx.itemName}</h1>
          <div className="flex items-center gap-2 mt-1 flex-wrap">
            {craftInfo && (
              <span className={`text-sm font-medium ${craftInfo.colorClass}`}>
                {craftInfo.fullName}
              </span>
            )}
            {recipe && (
              <span className="text-text-muted text-sm">Lv. {recipe.level}</span>
            )}
          </div>
        </div>
      </div>

      {/* Two-column on desktop */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Left column */}
        <div className="space-y-4">
          {/* Stat Check */}
          {journey.sufficiency && (
            <StatCheck sufficiency={journey.sufficiency} recipeId={recipeId} />
          )}

          {/* Gear Upgrades */}
          {journey.gearUpgrades.length > 0 && (
            <GearUpgrades upgrades={journey.gearUpgrades} />
          )}
        </div>

        {/* Right column */}
        <div className="space-y-4">
          {/* Materials / Shopping List */}
          <ShoppingListView
            shoppingList={materials.shoppingList}
            crystalRequirements={materials.crystalRequirements}
            intermediateCrafts={materials.intermediateCrafts}
            itemName={ctx.itemName}
            quantity={materials.summary.requestedQuantity}
            summary={materials.summary}
            recipeId={recipeId}
          />
        </div>
      </div>

      {/* Full-width sections */}
      <CostEstimate recipeId={recipeId} quantity={materials.summary.requestedQuantity} />

      <GatheringGuide
        shoppingList={materials.shoppingList}
        crystals={materials.crystalRequirements}
      />

      {/* Bottom sticky action bar */}
      <div className="fixed bottom-16 left-0 right-0 z-30 bg-bg-surface/95 backdrop-blur border-t border-border px-4 py-3">
        <div className="max-w-4xl mx-auto flex items-center justify-end gap-2">
          <button
            onClick={handleCopyShoppingList}
            className="flex items-center gap-1.5 px-3 py-2 text-xs font-medium rounded-md bg-bg-elevated text-text-secondary hover:text-gold transition-colors"
          >
            <Copy size={14} />
            Copy List
          </button>
          <Link
            href={`/simulator?recipeId=${recipeId}`}
            className="flex items-center gap-1.5 px-3 py-2 text-xs font-medium rounded-md bg-bg-elevated text-text-secondary hover:text-gold transition-colors"
          >
            <FlaskConical size={14} />
            Simulate
          </Link>
          <Link
            href={`/recipes/plan?add=${recipeId}`}
            className="flex items-center gap-1.5 px-3 py-2 text-xs font-medium rounded-md bg-gold/20 text-gold hover:bg-gold/30 transition-colors"
          >
            <ClipboardList size={14} />
            Add to Plan
          </Link>
        </div>
      </div>
    </div>
  );
}
