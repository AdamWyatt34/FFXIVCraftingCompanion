"use client";

import { useState, useCallback } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import {
  ArrowLeft,
  Copy,
  Wrench,
  ThumbsUp,
  Trash2,
  Link2,
  Check,
  BookOpen,
  Search,
} from "lucide-react";
import { useRotationDetail } from "@/hooks/useRotationDetail";
import { useAuth } from "@/contexts/AuthContext";
import {
  toggleRotationUpvote,
  deleteRotation,
  exportCraftingMacro,
} from "@/lib/api-client";
import {
  getActionIconUrlById,
  getActionNameById,
  ACTION_CATEGORY_COLORS,
} from "@/lib/action-icons";
import { CRAFT_TYPE_INFO, ROTATION_SOURCE_INFO } from "@/lib/constants";
import { LoadingSpinner } from "@/components/ui/LoadingSpinner";
import type { CraftType } from "@/types/api";
import type { ActionCategory } from "@/types/simulator";

function getQualityColor(pct: number): string {
  if (pct >= 90) return "text-emerald-400";
  if (pct >= 60) return "text-amber-400";
  return "text-red-400";
}

interface CollapsedGroup {
  actionId: number;
  actionName: string;
  count: number;
}

function collapseActionIds(actionIds: number[], actionNames: string[]): CollapsedGroup[] {
  const groups: CollapsedGroup[] = [];
  for (let i = 0; i < actionIds.length; i++) {
    const id = actionIds[i];
    const name = actionNames[i] ?? getActionNameById(id);
    const last = groups[groups.length - 1];
    if (last && last.actionId === id) {
      last.count++;
    } else {
      groups.push({ actionId: id, actionName: name, count: 1 });
    }
  }
  return groups;
}

export default function RotationDetailPage() {
  const params = useParams();
  const router = useRouter();
  const { user } = useAuth();
  const id = Number(params.id);
  const { rotation, isLoading, error, retry } = useRotationDetail(id);

  const [upvoteLoading, setUpvoteLoading] = useState(false);
  const [localUpvotes, setLocalUpvotes] = useState<number | null>(null);
  const [localUpvoted, setLocalUpvoted] = useState(false);
  const [macroCopied, setMacroCopied] = useState(false);
  const [shareCopied, setShareCopied] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [deleteConfirm, setDeleteConfirm] = useState(false);

  const handleUpvote = useCallback(async () => {
    if (!user || upvoteLoading) return;
    setUpvoteLoading(true);
    try {
      const result = await toggleRotationUpvote(id);
      setLocalUpvotes(result.upvotes);
      setLocalUpvoted(result.upvoted);
    } catch {
      // Ignore errors
    } finally {
      setUpvoteLoading(false);
    }
  }, [id, user, upvoteLoading]);

  const handleCopyMacro = useCallback(async () => {
    if (!rotation) return;
    try {
      const macroText =
        rotation.macros.length > 0
          ? rotation.macros.join("\n\n")
          : (
              await exportCraftingMacro({ actionIds: rotation.actionIds })
            ).macros.join("\n\n");
      await navigator.clipboard.writeText(macroText);
      setMacroCopied(true);
      setTimeout(() => setMacroCopied(false), 2000);
    } catch {
      // Ignore clipboard errors
    }
  }, [rotation]);

  const handleShare = useCallback(async () => {
    const url = `${window.location.origin}/simulator/library/${id}`;
    try {
      await navigator.clipboard.writeText(url);
      setShareCopied(true);
      setTimeout(() => setShareCopied(false), 2000);
    } catch {
      // Ignore
    }
  }, [id]);

  const handleLoad = useCallback(() => {
    if (!rotation) return;
    const params = new URLSearchParams({
      recipeId: String(rotation.recipeId),
      craftsmanship: String(rotation.craftsmanship),
      control: String(rotation.control),
      cp: String(rotation.cp),
      level: String(rotation.crafterLevel),
      rotation: rotation.actionIds.join(","),
    });
    router.push(`/simulator?${params.toString()}`);
  }, [rotation, router]);

  const handleDelete = useCallback(async () => {
    if (!deleteConfirm) {
      setDeleteConfirm(true);
      return;
    }
    setDeleting(true);
    try {
      await deleteRotation(id);
      router.push("/simulator/library");
    } catch {
      setDeleting(false);
      setDeleteConfirm(false);
    }
  }, [id, deleteConfirm, router]);

  // Loading state
  if (isLoading) {
    return (
      <div className="p-4">
        <div className="max-w-3xl mx-auto">
          <div className="flex items-center gap-3 mb-6">
            <Link
              href="/simulator/library"
              className="text-text-muted hover:text-text-primary transition-colors"
            >
              <ArrowLeft size={20} />
            </Link>
            <BookOpen className="text-gold" size={24} />
            <h1 className="text-xl font-bold">Loading...</h1>
          </div>
          <div className="flex items-center justify-center py-16">
            <LoadingSpinner size={32} />
          </div>
        </div>
      </div>
    );
  }

  // Error state
  if (error || !rotation) {
    return (
      <div className="p-4">
        <div className="max-w-3xl mx-auto">
          <div className="flex items-center gap-3 mb-6">
            <Link
              href="/simulator/library"
              className="text-text-muted hover:text-text-primary transition-colors"
            >
              <ArrowLeft size={20} />
            </Link>
            <BookOpen className="text-gold" size={24} />
            <h1 className="text-xl font-bold">Rotation</h1>
          </div>
          <div className="text-center py-16">
            <p className="text-red-400 mb-3">
              {error ?? "Rotation not found"}
            </p>
            <button
              onClick={retry}
              className="text-sm text-gold hover:text-gold-light transition-colors"
            >
              Try Again
            </button>
          </div>
        </div>
      </div>
    );
  }

  const craftInfo = CRAFT_TYPE_INFO[rotation.craftType as CraftType];
  const sourceInfo =
    ROTATION_SOURCE_INFO[rotation.source] ?? ROTATION_SOURCE_INFO.User;
  const groups = collapseActionIds(rotation.actionIds, rotation.actionNames);
  const upvotes = localUpvotes ?? rotation.upvotes;

  return (
    <div className="p-4">
      <div className="max-w-3xl mx-auto space-y-6">
        {/* Header */}
        <div className="flex items-center gap-3">
          <Link
            href="/simulator/library"
            className="text-text-muted hover:text-text-primary transition-colors"
          >
            <ArrowLeft size={20} />
          </Link>
          <BookOpen className="text-gold" size={24} />
          <h1 className="text-xl font-bold truncate flex-1">
            {rotation.name ?? rotation.recipeName}
          </h1>
        </div>

        {/* Summary card */}
        <div className="bg-bg-surface border border-border rounded-lg p-5 space-y-4">
          {/* Top metrics */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <div className="bg-bg-elevated rounded-md p-3 text-center">
              <div
                className={`text-xl font-bold ${getQualityColor(rotation.hqPercent)}`}
              >
                {rotation.hqPercent}%
              </div>
              <div className="text-xs text-text-muted mt-0.5">HQ</div>
            </div>
            <div className="bg-bg-elevated rounded-md p-3 text-center">
              <div className="text-xl font-bold text-text-primary">
                {Math.round(rotation.qualityPercent)}%
              </div>
              <div className="text-xs text-text-muted mt-0.5">Quality</div>
            </div>
            <div className="bg-bg-elevated rounded-md p-3 text-center">
              <div className="text-xl font-bold text-text-primary">
                {rotation.totalSteps}
              </div>
              <div className="text-xs text-text-muted mt-0.5">Steps</div>
            </div>
            <div className="bg-bg-elevated rounded-md p-3 text-center">
              <div
                className={`text-xl font-bold ${rotation.craftCompleted ? "text-emerald-400" : "text-red-400"}`}
              >
                {rotation.craftCompleted ? "Yes" : "No"}
              </div>
              <div className="text-xs text-text-muted mt-0.5">Completed</div>
            </div>
          </div>

          {/* Recipe info row */}
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-text-primary font-medium">
              {rotation.recipeName}
            </span>
            {craftInfo && (
              <span
                className={`text-xs px-2 py-0.5 rounded-full bg-bg-elevated ${craftInfo.colorClass}`}
              >
                {craftInfo.name}
              </span>
            )}
            <span className="text-sm text-text-muted">
              Lv.{rotation.recipeLevel}
            </span>
            <span
              className={`px-2 py-0.5 rounded text-xs font-medium ${sourceInfo.bgClass} ${sourceInfo.textClass}`}
            >
              {sourceInfo.label}
            </span>
            {rotation.algorithm && (
              <span className="text-xs text-text-muted">
                ({rotation.algorithm})
              </span>
            )}
          </div>

          {/* Stats */}
          <div className="flex items-center gap-4 text-sm text-text-secondary">
            <span>
              {rotation.craftsmanship} Craftsmanship / {rotation.control}{" "}
              Control / {rotation.cp} CP
            </span>
            {rotation.authorName && (
              <>
                <span className="text-border">|</span>
                <span>by {rotation.authorName}</span>
              </>
            )}
          </div>

          {/* Notes */}
          {rotation.notes && (
            <div className="bg-bg-elevated rounded-md p-3">
              <div className="text-xs font-medium text-text-muted mb-1">
                Notes
              </div>
              <p className="text-sm text-text-secondary whitespace-pre-wrap">
                {rotation.notes}
              </p>
            </div>
          )}
        </div>

        {/* Rotation strip */}
        <div className="bg-bg-surface border border-border rounded-lg p-5">
          <div className="text-xs font-medium text-text-muted mb-2">
            Rotation ({rotation.totalSteps} steps)
          </div>
          <div
            className="flex flex-wrap gap-1.5"
            role="list"
            aria-label="Rotation actions"
          >
            {groups.map((group, i) => {
              const category = getCategoryForAction(group.actionName);
              const color =
                ACTION_CATEGORY_COLORS[category] ??
                ACTION_CATEGORY_COLORS.Other;
              const iconUrl = getActionIconUrlById(group.actionId);
              const label =
                group.count > 1
                  ? `${group.actionName} x${group.count}`
                  : group.actionName;

              return (
                <span
                  key={`${group.actionId}-${i}`}
                  title={label}
                  className="flex items-center gap-1 px-2 py-1 rounded text-xs font-medium"
                  style={{
                    backgroundColor: `${color}15`,
                    borderLeft: `3px solid ${color}`,
                    color,
                  }}
                  role="listitem"
                >
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={iconUrl}
                    alt=""
                    width={16}
                    height={16}
                    className="rounded-sm"
                  />
                  <span>{label}</span>
                </span>
              );
            })}
          </div>
        </div>

        {/* Macros */}
        {rotation.macros.length > 0 && (
          <div className="bg-bg-surface border border-border rounded-lg p-5">
            <div className="text-xs font-medium text-text-muted mb-2">
              In-Game Macros ({rotation.macros.length})
            </div>
            <div className="space-y-3">
              {rotation.macros.map((macro, i) => (
                <pre
                  key={i}
                  className="bg-bg-elevated rounded-md p-3 text-xs text-text-secondary overflow-x-auto whitespace-pre-wrap"
                >
                  {macro}
                </pre>
              ))}
            </div>
          </div>
        )}

        {/* Action buttons */}
        <div className="flex flex-wrap gap-2">
          <button
            onClick={handleLoad}
            className="flex items-center gap-1.5 px-4 py-2 bg-gold text-bg-primary font-bold rounded-lg hover:bg-gold-light transition-colors text-sm"
          >
            <Wrench size={16} />
            Load in Simulator
          </button>
          <button
            onClick={handleCopyMacro}
            className="flex items-center gap-1.5 px-4 py-2 bg-bg-surface border border-border text-text-secondary font-medium rounded-lg hover:text-text-primary transition-colors text-sm"
          >
            <Copy size={16} />
            {macroCopied ? "Copied!" : "Copy Macro"}
          </button>
          <button
            onClick={handleShare}
            className="flex items-center gap-1.5 px-4 py-2 bg-bg-surface border border-border text-text-secondary font-medium rounded-lg hover:text-text-primary transition-colors text-sm"
          >
            {shareCopied ? (
              <>
                <Check size={16} className="text-emerald-400" />
                <span className="text-emerald-400">Copied!</span>
              </>
            ) : (
              <>
                <Link2 size={16} />
                Share URL
              </>
            )}
          </button>

          {/* Upvote */}
          <button
            onClick={handleUpvote}
            disabled={!user || upvoteLoading}
            className={`flex items-center gap-1.5 px-4 py-2 border rounded-lg font-medium transition-colors text-sm ml-auto ${
              localUpvoted
                ? "bg-gold/10 border-gold/30 text-gold"
                : "bg-bg-surface border-border text-text-muted hover:text-gold"
            } disabled:opacity-50 disabled:cursor-not-allowed`}
            aria-label={`Upvote (${upvotes})`}
          >
            <ThumbsUp size={16} />
            <span>{upvotes}</span>
          </button>
        </div>

        {/* Find similar + Delete row */}
        <div className="flex items-center gap-3">
          <Link
            href={`/simulator/library?recipeId=${rotation.recipeId}`}
            className="flex items-center gap-1.5 text-sm text-text-muted hover:text-text-primary transition-colors"
          >
            <Search size={14} />
            Find similar rotations
          </Link>

          {/* Delete button (visible for all authed users, server validates ownership) */}
          {user && (
            <button
              onClick={handleDelete}
              disabled={deleting}
              className="flex items-center gap-1.5 text-sm text-red-400 hover:text-red-300 transition-colors ml-auto disabled:opacity-50"
            >
              <Trash2 size={14} />
              {deleting
                ? "Deleting..."
                : deleteConfirm
                  ? "Confirm Delete?"
                  : "Delete"}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

/** Rough category lookup for styling. */
function getCategoryForAction(name: string): ActionCategory {
  const progress = [
    "Basic Synthesis",
    "Careful Synthesis",
    "Rapid Synthesis",
    "Focused Synthesis",
    "Groundwork",
    "Intensive Synthesis",
    "Prudent Synthesis",
    "Muscle Memory",
    "Delicate Synthesis",
  ];
  const quality = [
    "Basic Touch",
    "Standard Touch",
    "Advanced Touch",
    "Hasty Touch",
    "Focused Touch",
    "Preparatory Touch",
    "Prudent Touch",
    "Reflect",
    "Byregot's Blessing",
    "Trained Finesse",
    "Trained Eye",
  ];
  const repair = ["Master's Mend", "Immaculate Mend"];

  if (progress.includes(name)) return "Progress";
  if (quality.includes(name)) return "Quality";
  if (repair.includes(name)) return "Repair";

  // Check PascalCase variants
  const pascal = name.replace(/\s+/g, "");
  if (progress.some((p) => p.replace(/\s+/g, "") === pascal)) return "Progress";
  if (quality.some((q) => q.replace(/\s+/g, "") === pascal)) return "Quality";
  if (repair.some((r) => r.replace(/\s+/g, "") === pascal)) return "Repair";

  // Buffs and other
  const other = ["Tricks of the Trade", "Careful Observation", "TricksOfTheTrade", "CarefulObservation"];
  if (other.includes(name) || other.includes(pascal)) return "Other";

  return "Buff";
}
