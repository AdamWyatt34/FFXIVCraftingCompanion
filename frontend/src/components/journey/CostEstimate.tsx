"use client";

import { useState } from "react";
import { ChevronDown, ChevronRight, DollarSign, RefreshCw, AlertTriangle } from "lucide-react";
import { useWorldList, useRecipeCost } from "@/hooks/useMarket";
import { LoadingSpinner, ErrorState } from "@/components/ui";
import { WorldSelector } from "@/components/market/WorldSelector";
import type { CostEstimateDto, MaterialCostDto } from "@/types/market";

interface CostEstimateProps {
  recipeId: number;
  quantity?: number;
}

export function CostEstimate({ recipeId, quantity = 1 }: CostEstimateProps) {
  const [isExpanded, setIsExpanded] = useState(true);
  const [useMinPrice, setUseMinPrice] = useState(false);
  const [includeGathered, setIncludeGathered] = useState(true);
  const { selectedWorld, setSelectedWorld } = useWorldList();
  const { cost, isLoading, error, fetchCost } = useRecipeCost(recipeId, quantity, {
    useMinPrice,
    includeGatheredItemPrices: includeGathered,
  });

  const handleFetch = () => {
    if (selectedWorld) fetchCost(selectedWorld);
  };

  return (
    <div className="bg-bg-surface rounded-lg border border-border overflow-hidden">
      <button
        onClick={() => setIsExpanded(!isExpanded)}
        aria-expanded={isExpanded}
        className="w-full flex items-center gap-2 px-4 py-3 bg-bg-elevated/50 border-b border-border hover:bg-bg-elevated transition-colors min-h-[44px]"
      >
        {isExpanded ? (
          <ChevronDown size={16} className="text-text-muted shrink-0" />
        ) : (
          <ChevronRight size={16} className="text-text-muted shrink-0" />
        )}
        <DollarSign size={16} className="text-amber-400" />
        <h3 className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
          Cost Estimate
        </h3>
      </button>

      {isExpanded && (
        <div className="p-4 space-y-4">
          {/* World selector + fetch button */}
          <div className="flex items-center gap-2">
            <label className="flex-1 min-w-0">
              <span className="sr-only">World or data center</span>
              <WorldSelector
                value={selectedWorld}
                onChange={setSelectedWorld}
                className="w-full"
              />
            </label>
            <button
              onClick={handleFetch}
              disabled={!selectedWorld || isLoading}
              className="px-4 py-2 text-sm font-medium rounded-md bg-gold/20 text-gold hover:bg-gold/30 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isLoading ? <LoadingSpinner size={16} /> : "Fetch Prices"}
            </button>
            {cost && (
              <button
                onClick={handleFetch}
                disabled={isLoading}
                className="p-2 text-text-muted hover:text-gold transition-colors disabled:opacity-50"
                aria-label="Refresh prices"
              >
                <RefreshCw size={16} className={isLoading ? "animate-spin" : ""} />
              </button>
            )}
          </div>

          {/* Price mode toggle */}
          <div className="flex items-center gap-3">
            <div className="flex rounded-md border border-border overflow-hidden text-xs">
              <button
                onClick={() => setUseMinPrice(false)}
                className={`px-3 py-1.5 transition-colors ${!useMinPrice ? "bg-gold/20 text-gold" : "bg-bg-elevated text-text-muted hover:text-text-secondary"}`}
              >
                Average
              </button>
              <button
                onClick={() => setUseMinPrice(true)}
                className={`px-3 py-1.5 transition-colors ${useMinPrice ? "bg-gold/20 text-gold" : "bg-bg-elevated text-text-muted hover:text-text-secondary"}`}
              >
                Cheapest
              </button>
            </div>
            <label className="flex items-center gap-1.5 text-xs text-text-muted cursor-pointer">
              <input
                type="checkbox"
                checked={includeGathered}
                onChange={(e) => setIncludeGathered(e.target.checked)}
                className="accent-gold"
              />
              Include gathered item prices
            </label>
          </div>

          {error && <ErrorState message={error} />}

          {cost && <CostBreakdown cost={cost} quantity={quantity} />}

          {includeGathered && cost && (
            <p className="text-[10px] text-text-muted italic">
              Gathered items show market board prices as opportunity cost.
            </p>
          )}
        </div>
      )}
    </div>
  );
}

function staleLabelFor(isoDate?: string): string | null {
  if (!isoDate) return null;
  const diff = Date.now() - new Date(isoDate).getTime();
  const hours = Math.floor(diff / 3_600_000);
  if (hours < 24) return null;
  if (hours < 48) return `~${hours}h old`;
  const days = Math.floor(hours / 24);
  return `~${days}d old`;
}

function CostBreakdown({ cost, quantity }: { cost: CostEstimateDto; quantity: number }) {
  const [showAll, setShowAll] = useState(false);
  const displayMats = showAll ? cost.materialCosts : cost.materialCosts.slice(0, 30);
  const hasMore = cost.materialCosts.length > 30;

  return (
    <div className="space-y-3">
      {/* Warnings */}
      {cost.warnings && cost.warnings.length > 0 && (
        <div className="rounded-md border border-amber-500/20 bg-amber-500/5 p-3">
          {cost.warnings.map((w, i) => (
            <p key={i} className="text-xs text-amber-400 flex items-start gap-1.5">
              <AlertTriangle size={14} className="shrink-0 mt-0.5" />
              {w}
            </p>
          ))}
        </div>
      )}

      {/* Material table */}
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-text-muted text-xs uppercase border-b border-border">
              <th className="text-left py-2">Material</th>
              <th className="text-right py-2">Qty</th>
              <th className="text-right py-2">Unit</th>
              <th className="text-right py-2">Total</th>
              <th className="text-right py-2">Source</th>
            </tr>
          </thead>
          <tbody>
            {displayMats.map((mat) => (
              <MaterialRow key={mat.itemId} mat={mat} />
            ))}
          </tbody>
        </table>
      </div>

      {hasMore && !showAll && (
        <button
          onClick={() => setShowAll(true)}
          className="text-xs text-gold hover:text-gold-light transition-colors"
        >
          Show all {cost.materialCosts.length} materials
        </button>
      )}

      {/* Summary */}
      <div className="bg-bg-elevated/50 rounded-md p-3 space-y-1">
        <div className="flex justify-between text-sm">
          <span className="text-text-muted">Vendor Cost</span>
          <span className="text-amber-400 font-mono">{cost.totalVendorCost.toLocaleString()} Gil</span>
        </div>
        <div className="flex justify-between text-sm">
          <span className="text-text-muted">Market Board</span>
          <span className="text-blue-400 font-mono">{cost.totalMarketBoardCost.toLocaleString()} Gil</span>
        </div>
        <div className="flex justify-between text-sm font-semibold border-t border-border pt-1">
          <span className="text-text-primary">Total</span>
          <span className="text-gold font-mono">{cost.totalEstimatedCost.toLocaleString()} Gil</span>
        </div>
      </div>

      {/* Craft vs Buy */}
      {cost.craftingIsCheaper !== null && cost.finalItemMarketPrice !== null && (
        <div className={`rounded-md p-3 text-sm ${cost.craftingIsCheaper ? "bg-emerald-500/10 border border-emerald-500/20" : "bg-red-500/10 border border-red-500/20"}`}>
          <span className={cost.craftingIsCheaper ? "text-emerald-400" : "text-red-400"}>
            {cost.craftingIsCheaper
              ? `Crafting saves ~${cost.savings?.toLocaleString() ?? 0} Gil vs buying`
              : `Buying is cheaper (MB: ${buyPriceLabel(cost.finalItemMarketPrice, quantity)})`
            }
          </span>
        </div>
      )}
    </div>
  );
}

function buyPriceLabel(unitPrice: number, quantity: number): string {
  const unit = `${Math.round(unitPrice).toLocaleString()} Gil`;
  if (quantity <= 1) return unit;
  return `${quantity} × ${unit} = ${Math.round(unitPrice * quantity).toLocaleString()} Gil`;
}

function MaterialRow({ mat }: { mat: MaterialCostDto }) {
  const staleLabel = staleLabelFor(mat.lastUploadTime ?? undefined);
  const isNonTradeable = mat.priceSource === "Unknown" && mat.listingCount === 0;

  return (
    <tr className="border-b border-border/30">
      <td className="py-1.5 text-text-primary truncate max-w-[150px]">{mat.itemName}</td>
      <td className="py-1.5 text-right text-text-muted font-mono">{mat.quantity}</td>
      <td className="py-1.5 text-right text-text-muted font-mono">
        {isNonTradeable ? "—" : mat.unitPrice.toLocaleString()}
      </td>
      <td className="py-1.5 text-right text-amber-400 font-mono">
        {isNonTradeable ? (
          <span className="text-text-muted text-xs">Not tradeable</span>
        ) : (
          mat.totalPrice.toLocaleString()
        )}
      </td>
      <td className="py-1.5 text-right">
        <span className={`text-xs ${mat.priceSource === "Vendor" ? "text-amber-400" : mat.priceSource === "MarketBoard" ? "text-blue-400" : "text-text-muted"}`}>
          {mat.priceSource === "NonGil" ? "Non-Gil" : mat.priceSource}
        </span>
        {mat.isStalePrice && staleLabel && (
          <span className="inline-flex items-center ml-1" title={`Price data ${staleLabel}`}>
            <AlertTriangle size={12} className="text-amber-400" />
          </span>
        )}
      </td>
    </tr>
  );
}
