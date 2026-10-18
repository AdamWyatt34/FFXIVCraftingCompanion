"use client";

import { useState, useEffect } from "react";
import { ChevronDown, ChevronRight, Pickaxe, Clock } from "lucide-react";
import { ApiClientError } from "@/lib/api-client";
import { getGatheringGuide, getEorzeanTime } from "@/lib/game-data";
import { ItemIcon, LoadingSpinner, ErrorState } from "@/components/ui";
import { GATHERING_CLASS_INFO, NODE_TYPE_INFO } from "@/lib/constants";
import { TimedNodeCountdown } from "@/components/notifications/TimedNodeCountdown";
import type { GatheringGuideDto, TimedNodeEntryDto } from "@/types/gathering";

interface GatherableItem {
  itemId: number;
  totalQuantity: number;
  primarySource: string;
}

interface GatheringGuideProps {
  shoppingList: GatherableItem[];
  crystals: GatherableItem[];
}

export function GatheringGuide({ shoppingList, crystals }: GatheringGuideProps) {
  const [isExpanded, setIsExpanded] = useState(false);
  const [guide, setGuide] = useState<GatheringGuideDto | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [eorzeanHour, setEorzeanHour] = useState<number | null>(null);

  useEffect(() => {
    if (!isExpanded || guide) return;

    const gathered = shoppingList
      .filter((i) => i.primarySource === "Gathered")
      .map((i) => ({ itemId: i.itemId, quantity: i.totalQuantity }));
    const crystalItems = crystals.map((c) => ({
      itemId: c.itemId,
      quantity: c.totalQuantity,
    }));
    const allItems = [...gathered, ...crystalItems];

    if (allItems.length === 0) return;

    let cancelled = false;
    (async () => {
      setIsLoading(true);
      try {
        const [guideData, timeData] = await Promise.all([
          getGatheringGuide(allItems),
          getEorzeanTime().catch(() => null),
        ]);
        if (!cancelled) {
          setGuide(guideData);
          if (timeData) setEorzeanHour(timeData.currentEorzeanHour);
        }
      } catch (err: unknown) {
        if (!cancelled) {
          const msg =
            err instanceof ApiClientError
              ? err.message
              : "Failed to load gathering guide.";
          setError(msg);
        }
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [isExpanded, guide, shoppingList, crystals]);

  return (
    <div className="bg-bg-surface rounded-lg border border-border overflow-hidden">
      <button
        onClick={() => setIsExpanded(!isExpanded)}
        className="w-full flex items-center gap-2 px-4 py-3 bg-bg-elevated/50 border-b border-border hover:bg-bg-elevated transition-colors min-h-[44px]"
      >
        {isExpanded ? (
          <ChevronDown size={16} className="text-text-muted shrink-0" />
        ) : (
          <ChevronRight size={16} className="text-text-muted shrink-0" />
        )}
        <Pickaxe size={16} className="text-emerald-400" />
        <h3 className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
          Gathering Guide
        </h3>
      </button>

      {isExpanded && (
        <div className="p-4 space-y-4">
          {isLoading && (
            <div className="flex items-center justify-center py-6">
              <LoadingSpinner size={24} />
            </div>
          )}

          {error && <ErrorState message={error} />}

          {guide && (
            <>
              {/* Timed nodes alert */}
              {guide.timedNodes.length > 0 && (
                <TimedNodesSection
                  timedNodes={guide.timedNodes}
                  eorzeanHour={eorzeanHour}
                />
              )}

              {/* Zone groups */}
              <div className="space-y-3">
                {guide.zoneGroups.map((zone) => (
                  <div key={zone.zoneName} className="space-y-1">
                    <h4 className="text-xs font-semibold text-text-muted uppercase tracking-wider">
                      {zone.zoneName}
                    </h4>
                    <div className="space-y-1">
                      {zone.items.map((item) => {
                        const classInfo = GATHERING_CLASS_INFO[item.gatheringClass];
                        const nodeInfo = NODE_TYPE_INFO[item.nodeType];
                        return (
                          <div
                            key={item.itemId}
                            className="flex items-center gap-2 px-3 py-2 rounded-md bg-bg-elevated/30"
                          >
                            <ItemIcon iconUrl={item.iconUrl} alt={item.itemName} size="sm" />
                            <span className="flex-1 text-sm text-text-primary truncate">
                              {item.itemName}
                            </span>
                            <span className="text-xs text-text-muted font-mono">x{item.quantity}</span>
                            <span className={`text-xs ${classInfo?.colorClass ?? "text-text-muted"}`}>
                              {classInfo?.label ?? item.gatheringClass} {item.gatheringLevel}
                            </span>
                            {nodeInfo && item.nodeType !== "Standard" && (
                              <span className={`text-xs ${nodeInfo.colorClass}`}>
                                {nodeInfo.label}
                              </span>
                            )}
                            {item.coordinateX != null && item.coordinateY != null && (
                              <span className="text-xs text-text-muted">
                                ({item.coordinateX.toFixed(1)}, {item.coordinateY.toFixed(1)})
                              </span>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  </div>
                ))}
              </div>

              {/* Summary */}
              <div className="text-xs text-text-muted">
                {guide.summary.totalZones} zones &middot; {guide.summary.totalItems} items
                {guide.summary.timedNodeCount > 0 && (
                  <> &middot; {guide.summary.timedNodeCount} timed</>
                )}
              </div>
            </>
          )}
        </div>
      )}
    </div>
  );
}

function TimedNodesSection({
  timedNodes,
  eorzeanHour,
}: {
  timedNodes: TimedNodeEntryDto[];
  eorzeanHour: number | null;
}) {
  return (
    <div className="rounded-md border border-amber-500/20 bg-amber-500/5 p-3 space-y-2">
      <div className="flex items-center gap-2">
        <Clock size={14} className="text-amber-400" />
        <span className="text-xs font-semibold text-amber-400 uppercase">
          Timed Nodes
          {eorzeanHour !== null && (
            <span className="font-normal ml-1">(ET: {eorzeanHour}:00)</span>
          )}
        </span>
      </div>
      {timedNodes.map((node) => (
        <div
          key={`${node.itemId}-${node.startHour}`}
          className="flex items-center gap-2 px-2 py-1.5 rounded text-sm"
        >
          <ItemIcon iconUrl={node.iconUrl} alt={node.itemName} size="sm" />
          <span className="flex-1 truncate text-text-primary">{node.itemName}</span>
          <span className="text-xs text-text-muted">x{node.quantity}</span>
          <TimedNodeCountdown startHour={node.startHour} endHour={node.endHour} />
        </div>
      ))}
    </div>
  );
}
