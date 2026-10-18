"use client";

import { Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Info, ListOrdered, ShoppingCart, TreePine } from "lucide-react";
import { trackAnalyticsEvent } from "@/lib/analytics";
import { useBreadcrumbs } from "@/contexts/BreadcrumbContext";
import { ErrorState, LoadingSpinner } from "@/components/ui";
import { useMaterialTree } from "@/hooks/useMaterialTree";
import { MaterialTree } from "./MaterialTree";
import { QuantityMultiplier } from "./QuantityMultiplier";
import { ShoppingListView } from "./ShoppingListView";
import { CraftingStepsView } from "./CraftingStepsView";
import { CraftingStepsSkeleton, ShoppingListSkeleton } from "./ShoppingListSkeleton";
import type { RecipeEquipmentInfoDto } from "@/types/api";

type TabKey = "shopping" | "tree" | "steps";

const TABS: { key: TabKey; label: string; icon: typeof ShoppingCart }[] = [
  { key: "shopping", label: "Shopping List", icon: ShoppingCart },
  { key: "tree", label: "Crafting Tree", icon: TreePine },
  { key: "steps", label: "Crafting Steps", icon: ListOrdered },
];

function isValidTab(val: string | null): val is TabKey {
  return val === "shopping" || val === "tree" || val === "steps";
}

interface RecipeMaterialsProps {
  recipeId: number;
  itemName: string;
  equipInfo: RecipeEquipmentInfoDto | null;
}

/**
 * The interactive half of a recipe page: quantity, and the shopping list, crafting tree and
 * crafting steps recomputed from the cached export. The server renders the recipe itself; this
 * island only receives ids and names, so the RSC payload does not repeat the HTML.
 */
function RecipeMaterialsContent({ recipeId, itemName, equipInfo }: RecipeMaterialsProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [quantity, setQuantity] = useState(1);
  const { setBreadcrumbs } = useBreadcrumbs();

  useEffect(() => {
    setBreadcrumbs([{ label: "Recipes", href: "/recipes" }, { label: itemName }]);
  }, [itemName, setBreadcrumbs]);

  useEffect(() => {
    trackAnalyticsEvent("recipe_view", { recipeId, recipeName: itemName });
  }, [recipeId, itemName]);

  const tabParam = searchParams.get("tab");
  const activeTab: TabKey = isValidTab(tabParam) ? tabParam : "shopping";

  function setActiveTab(tab: TabKey) {
    const params = new URLSearchParams(searchParams.toString());
    params.set("tab", tab);
    router.replace(`?${params.toString()}`, { scroll: false });
  }

  const { data: treeData, isLoading: isTreeLoading, error: treeError, retry: retryTree } =
    useMaterialTree(recipeId, quantity);

  return (
    <>
      <QuantityMultiplier value={quantity} onChange={setQuantity} />

      {treeData && treeData.summary.maxTreeDepth > 5 && (
        <div className="flex items-center gap-2 px-4 py-2.5 rounded-lg bg-blue-500/10 border border-blue-500/20">
          <Info size={16} className="text-blue-400 shrink-0" />
          <span className="text-sm text-blue-300">
            This is a complex recipe with {treeData.summary.maxTreeDepth} levels of sub-crafts.
          </span>
        </div>
      )}

      <div className="border-b border-border">
        <div className="flex gap-0" role="tablist" aria-label="Materials">
          {TABS.map((tab) => {
            const Icon = tab.icon;
            const isActive = activeTab === tab.key;
            return (
              <button
                key={tab.key}
                role="tab"
                aria-selected={isActive}
                onClick={() => setActiveTab(tab.key)}
                className={`flex items-center gap-1.5 px-4 py-3 text-sm font-medium border-b-2 transition-colors ${
                  isActive
                    ? "border-gold text-gold"
                    : "border-transparent text-text-muted hover:text-text-secondary"
                }`}
              >
                <Icon size={16} />
                {tab.label}
              </button>
            );
          })}
        </div>
      </div>

      <div className="pb-4 min-h-[200px]">
        {isTreeLoading ? (
          <>
            {activeTab === "shopping" && <ShoppingListSkeleton />}
            {activeTab === "tree" && (
              <div className="flex items-center justify-center p-8 gap-3">
                <LoadingSpinner size={24} />
                <span className="text-sm text-text-muted">Loading tree...</span>
              </div>
            )}
            {activeTab === "steps" && <CraftingStepsSkeleton />}
          </>
        ) : treeError ? (
          <ErrorState message={treeError} onRetry={retryTree} />
        ) : treeData ? (
          <>
            {activeTab === "shopping" && (
              <ShoppingListView
                shoppingList={treeData.shoppingList}
                crystalRequirements={treeData.crystalRequirements}
                intermediateCrafts={treeData.intermediateCrafts}
                itemName={itemName}
                quantity={quantity}
                summary={treeData.summary}
                recipeId={recipeId}
              />
            )}
            {activeTab === "tree" && (
              <>
                {equipInfo && (
                  <div className="mb-3 px-3 py-2 rounded-md bg-gold/5 border border-gold/20 text-xs text-text-secondary">
                    This gear provides:{" "}
                    {[
                      equipInfo.craftsmanship > 0 && `Craftsmanship +${equipInfo.craftsmanship}`,
                      equipInfo.control > 0 && `Control +${equipInfo.control}`,
                      equipInfo.cp > 0 && `CP +${equipInfo.cp}`,
                      equipInfo.gathering > 0 && `Gathering +${equipInfo.gathering}`,
                      equipInfo.perception > 0 && `Perception +${equipInfo.perception}`,
                      equipInfo.gp > 0 && `GP +${equipInfo.gp}`,
                    ]
                      .filter(Boolean)
                      .join(", ")}
                  </div>
                )}
                <MaterialTree tree={treeData.tree} />
              </>
            )}
            {activeTab === "steps" && (
              <CraftingStepsView intermediateCrafts={treeData.intermediateCrafts} tree={treeData.tree} />
            )}
          </>
        ) : null}
      </div>
    </>
  );
}

export function RecipeMaterials(props: RecipeMaterialsProps) {
  return (
    <Suspense fallback={<ShoppingListSkeleton />}>
      <RecipeMaterialsContent {...props} />
    </Suspense>
  );
}
