"use client";

import { useState, useEffect, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { useBreadcrumbs } from "@/contexts/BreadcrumbContext";
import {
  ArrowLeft,
  Play,
  Save,
  Trash2,
  Download,
  Check,
  ShoppingCart,
  ListOrdered,
} from "lucide-react";
import { useCraftPlanContext } from "@/contexts/CraftPlanContext";
import { useCraftPlan } from "@/hooks/useCraftPlan";
import { useAuth } from "@/contexts/AuthContext";
import {
  PlanItemList,
  PlanSearchBar,
  PlanShoppingList,
  PlanCraftingOrder,
} from "@/components/craft-plan";
import { GatheringGuide } from "@/components/journey";
import { LoadingSpinner, ErrorState } from "@/components/ui";
import type { CraftPlanItemInput } from "@/types/craft-plan";

type ResultTab = "shopping" | "crafting";

function CraftPlanContent() {
  const searchParams = useSearchParams();
  const planId = searchParams.get("id");

  const { planItems, addToPlan, removeFromPlan, updateQuantity, clearPlan } =
    useCraftPlanContext();
  const {
    result,
    isResolving,
    resolveError,
    resolve,
    clearResult,
    savePlan,
    loadPlan,
    doExport,
    saveError,
  } = useCraftPlan();
  const { isAuthenticated } = useAuth();

  const [activeResultTab, setActiveResultTab] = useState<ResultTab>("shopping");
  const [saveName, setSaveName] = useState("");
  const [isSaving, setIsSaving] = useState(false);
  const [exportConfirmation, setExportConfirmation] = useState(false);
  const [loadedPlanName, setLoadedPlanName] = useState<string | null>(null);
  const { setBreadcrumbs } = useBreadcrumbs();

  useEffect(() => {
    setBreadcrumbs([
      { label: "Recipes", href: "/recipes" },
      { label: loadedPlanName ?? "Craft Plan" },
    ]);
  }, [loadedPlanName, setBreadcrumbs]);

  // Load saved plan if ?id= is present
  useEffect(() => {
    if (!planId) return;
    loadPlan(planId).then((data) => {
      if (data) {
        setLoadedPlanName(data.plan.name);
      }
    });
  }, [planId, loadPlan]);

  const existingRecipeIds = new Set(planItems.map((p) => p.recipeId));

  const handleResolve = () => {
    const items: CraftPlanItemInput[] = planItems.map((p) => ({
      recipeId: p.recipeId,
      quantity: p.quantity,
    }));
    resolve(items);
  };

  const handleSave = async () => {
    if (!saveName.trim() || planItems.length === 0) return;
    setIsSaving(true);
    const items: CraftPlanItemInput[] = planItems.map((p) => ({
      recipeId: p.recipeId,
      quantity: p.quantity,
    }));
    await savePlan(saveName.trim(), items);
    setIsSaving(false);
  };

  const handleExport = async () => {
    const items: CraftPlanItemInput[] = planItems.map((p) => ({
      recipeId: p.recipeId,
      quantity: p.quantity,
    }));
    const exported = await doExport(items, "text");
    if (exported?.content) {
      try {
        await navigator.clipboard.writeText(exported.content);
        setExportConfirmation(true);
        setTimeout(() => setExportConfirmation(false), 2000);
      } catch {
        // Clipboard API may fail outside secure contexts
      }
    }
  };

  const handleClear = () => {
    clearPlan();
    clearResult();
    setSaveName("");
    setActiveResultTab("shopping");
  };

  return (
    <div className="p-4 space-y-6 max-w-4xl mx-auto">
      {/* Header */}
      <div className="flex items-center gap-2 text-xs text-text-muted">
        <Link
          href="/recipes"
          className="flex items-center gap-1 text-text-secondary hover:text-gold transition-colors text-sm"
        >
          <ArrowLeft size={16} />
        </Link>
        <Link href="/recipes" className="hover:text-gold transition-colors">
          Recipes
        </Link>
        <span>&gt;</span>
        <span className="text-text-secondary">
          {loadedPlanName ?? "Craft Plan"}
        </span>
      </div>

      <h1 className="text-xl font-bold text-text-primary">
        {loadedPlanName ?? "Craft Plan"}
      </h1>

      {/* Two-column layout */}
      <div className="grid grid-cols-1 lg:grid-cols-[1fr_2fr] gap-6">
        {/* Left: Search + Item list */}
        <div className="space-y-4">
          <PlanSearchBar
            onAdd={(item) => addToPlan(item)}
            existingRecipeIds={existingRecipeIds}
          />
          {planItems.length === 0 && !planId ? (
            <div className="flex flex-col items-center gap-3 p-8 text-center bg-bg-surface rounded-lg border border-border">
              <p className="text-text-secondary text-sm">
                Your craft plan is empty. Search for recipes above to get started.
              </p>
              <Link
                href="/recipes"
                className="text-sm text-gold hover:text-gold-light transition-colors"
              >
                Browse Recipes
              </Link>
            </div>
          ) : (
            <PlanItemList
              items={planItems}
              onRemove={removeFromPlan}
              onUpdateQuantity={updateQuantity}
            />
          )}
        </div>

        {/* Right: Results */}
        <div className="space-y-4">
          {/* Actions bar */}
          <div className="flex flex-wrap items-center gap-2">
            <button
              onClick={handleResolve}
              disabled={planItems.length === 0 || isResolving}
              className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium rounded-md bg-gold/20 text-gold hover:bg-gold/30 transition-colors disabled:opacity-50"
            >
              {isResolving ? <LoadingSpinner size={14} /> : <Play size={14} />}
              Resolve
            </button>

            {isAuthenticated && (
              <div className="flex items-center gap-1">
                <input
                  type="text"
                  value={saveName}
                  onChange={(e) => setSaveName(e.target.value)}
                  placeholder="Plan name..."
                  className="px-3 py-2 text-sm bg-bg-elevated border border-border rounded-md text-text-primary w-36"
                />
                <button
                  onClick={handleSave}
                  disabled={!saveName.trim() || planItems.length === 0 || isSaving}
                  className="flex items-center gap-1 px-3 py-2 text-sm rounded-md bg-bg-elevated text-text-secondary hover:text-gold transition-colors disabled:opacity-50"
                >
                  <Save size={14} />
                </button>
              </div>
            )}

            <button
              onClick={handleExport}
              disabled={planItems.length === 0}
              className="flex items-center gap-1 px-3 py-2 text-sm rounded-md bg-bg-elevated text-text-secondary hover:text-gold transition-colors disabled:opacity-50"
            >
              {exportConfirmation ? (
                <Check size={14} className="text-emerald-400" />
              ) : (
                <Download size={14} />
              )}
              {exportConfirmation ? "Copied!" : "Export"}
            </button>

            <button
              onClick={handleClear}
              disabled={planItems.length === 0 && !result}
              className="flex items-center gap-1 px-3 py-2 text-sm rounded-md bg-bg-elevated text-text-muted hover:text-red-400 transition-colors disabled:opacity-50 ml-auto"
            >
              <Trash2 size={14} />
              Clear
            </button>
          </div>

          {saveError && (
            <p className="text-xs text-red-400">{saveError}</p>
          )}

          {resolveError && <ErrorState message={resolveError} />}

          {/* Result tabs */}
          {result && (
            <>
              <div className="border-b border-border">
                <div className="flex gap-0">
                  <button
                    onClick={() => setActiveResultTab("shopping")}
                    className={`flex items-center gap-1.5 px-4 py-3 text-sm font-medium border-b-2 transition-colors ${
                      activeResultTab === "shopping"
                        ? "border-gold text-gold"
                        : "border-transparent text-text-muted hover:text-text-secondary"
                    }`}
                  >
                    <ShoppingCart size={16} />
                    Shopping List
                  </button>
                  <button
                    onClick={() => setActiveResultTab("crafting")}
                    className={`flex items-center gap-1.5 px-4 py-3 text-sm font-medium border-b-2 transition-colors ${
                      activeResultTab === "crafting"
                        ? "border-gold text-gold"
                        : "border-transparent text-text-muted hover:text-text-secondary"
                    }`}
                  >
                    <ListOrdered size={16} />
                    Crafting Order
                  </button>
                </div>
              </div>

              {activeResultTab === "shopping" && (
                <PlanShoppingList
                  rawMaterials={result.rawMaterials}
                  crystals={result.crystals}
                />
              )}

              {activeResultTab === "crafting" && (
                <PlanCraftingOrder
                  intermediateCrafts={result.intermediateCrafts}
                  finalItems={result.items}
                />
              )}

              {/* Gathering Guide */}
              <GatheringGuide
                shoppingList={result.rawMaterials}
                crystals={result.crystals}
              />

              {/* Summary */}
              <div className="text-xs text-text-muted bg-bg-elevated/50 rounded-md px-3 py-2">
                {result.summary.totalPlanItems} items &middot;{" "}
                {result.summary.totalUniqueRawMaterials} materials &middot;{" "}
                {result.summary.totalUniqueCrystals} crystals &middot;{" "}
                {result.summary.totalIntermediateCrafts} sub-crafts
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

export default function CraftPlanPage() {
  return (
    <Suspense
      fallback={
        <div className="flex items-center justify-center p-12">
          <LoadingSpinner size={32} />
        </div>
      }
    >
      <CraftPlanContent />
    </Suspense>
  );
}
