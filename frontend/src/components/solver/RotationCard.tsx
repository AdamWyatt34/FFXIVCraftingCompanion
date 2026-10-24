"use client";

import { useState, useCallback } from "react";
import Image from "next/image";
import Link from "next/link";
import { Eye, Copy, Wrench, ThumbsUp, Map } from "lucide-react";
import { getActionIconUrlById } from "@/lib/action-icons";
import { CRAFT_TYPE_INFO, ROTATION_SOURCE_INFO } from "@/lib/constants";
import { exportCraftingMacro } from "@/lib/crafting-engine";
import type { RotationListItem } from "@/types/solver";
import type { CraftType } from "@/types/api";
import { recipeJourneyPath, rotationPath } from "@/lib/routes";

interface RotationCardProps {
  rotation: RotationListItem;
  onLoad: (rotation: RotationListItem) => void;
  onUpvote?: (id: number) => void;
  compact?: boolean;
}

const SAVED_METRIC_TITLE = "At save time; open the rotation for the current engine's numbers";

function getQualityColor(pct: number): string {
  if (pct >= 90) return "text-emerald-400";
  if (pct >= 60) return "text-amber-400";
  return "text-red-400";
}

function isNew(createdAt: string): boolean {
  const created = new Date(createdAt).getTime();
  const now = Date.now();
  return now - created < 24 * 60 * 60 * 1000;
}

export function RotationCard({
  rotation,
  onLoad,
  onUpvote,
  compact = false,
}: RotationCardProps) {
  const [macroCopied, setMacroCopied] = useState(false);

  const craftInfo = CRAFT_TYPE_INFO[rotation.craftType as CraftType];
  const sourceInfo =
    ROTATION_SOURCE_INFO[rotation.source] ?? ROTATION_SOURCE_INFO.User;

  const handleCopyMacro = useCallback(async () => {
    try {
      const result = await exportCraftingMacro({
        actionIds: rotation.actionIds,
      });
      const macroText = result.macros.join("\n\n");
      await navigator.clipboard.writeText(macroText);
      setMacroCopied(true);
      setTimeout(() => setMacroCopied(false), 2000);
    } catch {
      // Fallback: just copy action IDs
      try {
        await navigator.clipboard.writeText(rotation.actionIds.join(","));
        setMacroCopied(true);
        setTimeout(() => setMacroCopied(false), 2000);
      } catch {
        // Ignore clipboard errors
      }
    }
  }, [rotation.actionIds]);

  if (compact) {
    return (
      <div className="flex items-center gap-3 p-2 bg-bg-surface border border-border rounded-lg hover:border-gold/30 transition-colors">
        <span
          className={`text-sm font-bold ${getQualityColor(rotation.hqPercent)}`}
          title={SAVED_METRIC_TITLE}
        >
          {rotation.hqPercent}% HQ
        </span>
        <span className="text-xs text-text-muted" title={SAVED_METRIC_TITLE}>
          {rotation.totalSteps} steps
        </span>
        <span className="text-sm text-text-secondary truncate flex-1">
          {rotation.recipeName}
        </span>
        <Link
          href={rotationPath(rotation.id)}
          className="text-xs text-gold hover:text-gold-light transition-colors"
        >
          View
        </Link>
      </div>
    );
  }

  return (
    <div className="bg-bg-surface border border-border rounded-lg p-4 hover:border-gold/30 transition-colors space-y-3">
      {/* Top row: HQ%, steps, source badge */}
      <div className="flex items-center gap-2 flex-wrap">
        <span
          className={`text-lg font-bold ${getQualityColor(rotation.hqPercent)}`}
          title={SAVED_METRIC_TITLE}
        >
          {rotation.hqPercent}% HQ
        </span>
        <span className="text-sm text-text-muted" title={SAVED_METRIC_TITLE}>
          {rotation.totalSteps} steps
        </span>
        <span
          className={`px-2 py-0.5 rounded text-xs font-medium ${sourceInfo.bgClass} ${sourceInfo.textClass}`}
        >
          {sourceInfo.label}
        </span>
        {isNew(rotation.createdAt) && (
          <span className="px-2 py-0.5 rounded text-xs font-medium bg-emerald-500/20 text-emerald-400">
            New
          </span>
        )}
        {!rotation.craftCompleted && (
          <span
            className="px-2 py-0.5 rounded text-xs font-medium bg-red-500/20 text-red-400"
            title={SAVED_METRIC_TITLE}
          >
            Incomplete
          </span>
        )}
      </div>

      {/* Recipe info */}
      <div className="flex items-center gap-2 text-sm">
        <span className="text-text-primary font-medium truncate">
          {rotation.recipeName}
        </span>
        {craftInfo && (
          <span className={`text-xs font-medium ${craftInfo.colorClass}`}>
            {craftInfo.name}
          </span>
        )}
        <span className="text-text-muted">Lv.{rotation.recipeLevel}</span>
      </div>

      {/* Stats */}
      <div className="flex items-center gap-3 text-xs text-text-muted">
        <span>
          {rotation.craftsmanship}/{rotation.control}/{rotation.cp}
        </span>
        {rotation.authorName && (
          <>
            <span className="text-border">|</span>
            <span>by {rotation.authorName}</span>
          </>
        )}
      </div>

      {/* Action strip (abbreviated) */}
      <div className="flex flex-wrap gap-1 max-h-10 overflow-hidden">
        {rotation.actionIds.slice(0, 20).map((actionId, i) => {
          const iconUrl = getActionIconUrlById(actionId);
          return (
            <span key={`${actionId}-${i}`}>
              <Image
                src={iconUrl}
                alt=""
                width={20}
                height={20}
                className="rounded-sm"
                unoptimized
              />
            </span>
          );
        })}
        {rotation.actionIds.length > 20 && (
          <span className="text-xs text-text-muted self-center">
            +{rotation.actionIds.length - 20}
          </span>
        )}
      </div>

      {/* Action buttons */}
      <div className="grid grid-cols-2 sm:flex sm:flex-wrap gap-2 items-center">
        <Link
          href={rotationPath(rotation.id)}
          className="flex items-center gap-1 px-3 py-1.5 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-xs"
        >
          <Eye size={14} />
          View
        </Link>
        <button
          onClick={handleCopyMacro}
          className="flex items-center gap-1 px-3 py-1.5 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-xs"
        >
          <Copy size={14} />
          {macroCopied ? "Copied!" : "Copy Macro"}
        </button>
        <button
          onClick={() => onLoad(rotation)}
          className="flex items-center gap-1 px-3 py-1.5 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-xs"
        >
          <Wrench size={14} />
          Load in Simulator
        </button>
        <Link
          href={recipeJourneyPath(rotation.recipeId, rotation.recipeName)}
          className="flex items-center gap-1 px-3 py-1.5 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-gold border border-border transition-colors text-xs"
        >
          <Map size={14} />
          Journey
        </Link>

        {/* Upvote button */}
        <button
          onClick={() => onUpvote?.(rotation.id)}
          className="flex items-center gap-1 px-2 py-1.5 text-text-muted hover:text-gold transition-colors text-xs ml-auto"
          aria-label={`Upvote (${rotation.upvotes})`}
        >
          <ThumbsUp size={14} />
          <span>{rotation.upvotes}</span>
        </button>
      </div>
    </div>
  );
}
