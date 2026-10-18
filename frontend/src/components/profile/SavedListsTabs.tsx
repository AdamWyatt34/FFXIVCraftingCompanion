"use client";

import { useState } from "react";
import { Pin, Trash2, ShoppingCart, Shield, ClipboardList, Heart, ExternalLink, Map } from "lucide-react";
import { useSavedLists } from "@/hooks/useSavedLists";
import { useFavorites } from "@/hooks/useFavorites";
import { ItemIcon } from "@/components/ui/ItemIcon";
import { LoadingSpinner } from "@/components/ui/LoadingSpinner";
import Link from "next/link";
import type { SavedListSummaryDto } from "@/types/saved-lists";
import { recipeJourneyPath, recipePath } from "@/lib/routes";

const TABS = [
  { key: "ShoppingList", label: "Shopping Lists", icon: ShoppingCart },
  { key: "GearSet", label: "Gear Sets", icon: Shield },
  { key: "CraftingPlan", label: "Crafting Plans", icon: ClipboardList },
  { key: "favorites", label: "Favorites", icon: Heart },
] as const;

type TabKey = (typeof TABS)[number]["key"];

export function SavedListsTabs() {
  const [activeTab, setActiveTab] = useState<TabKey>("ShoppingList");

  return (
    <div className="space-y-4">
      {/* Tab bar */}
      <div className="flex gap-1 overflow-x-auto pb-1 -mx-1 px-1">
        {TABS.map((tab) => {
          const isActive = activeTab === tab.key;
          const Icon = tab.icon;
          return (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key)}
              className={`flex items-center gap-1.5 px-3 py-2 rounded-full text-xs font-medium whitespace-nowrap transition-colors shrink-0 ${
                isActive
                  ? "bg-gold/15 text-gold border border-gold/30"
                  : "bg-bg-surface text-text-muted border border-transparent hover:text-text-secondary"
              }`}
            >
              <Icon size={14} />
              {tab.label}
            </button>
          );
        })}
      </div>

      {/* Tab content */}
      {activeTab === "favorites" ? (
        <FavoritesTabContent />
      ) : (
        <SavedListsTabContent typeFilter={activeTab} />
      )}
    </div>
  );
}

function SavedListsTabContent({ typeFilter }: { typeFilter: string }) {
  const { lists, isLoading, error, deleteList, togglePin } = useSavedLists(typeFilter);
  const [deletingId, setDeletingId] = useState<number | null>(null);

  async function handleDelete(id: number) {
    setDeletingId(id);
    try {
      await deleteList(id);
    } finally {
      setDeletingId(null);
    }
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-8">
        <LoadingSpinner size={24} />
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-3 rounded-lg bg-red-500/10 border border-red-500/30 text-red-400 text-sm text-center">
        {error}
      </div>
    );
  }

  if (lists.length === 0) {
    return <EmptyListState type={typeFilter} />;
  }

  return (
    <div className="space-y-2">
      {lists.map((list) => (
        <SavedListItem
          key={list.id}
          list={list}
          isDeleting={deletingId === list.id}
          onDelete={() => handleDelete(list.id)}
          onTogglePin={() => togglePin(list.id, !list.isPinned)}
        />
      ))}
    </div>
  );
}

function SavedListItem({
  list,
  isDeleting,
  onDelete,
  onTogglePin,
}: {
  list: SavedListSummaryDto;
  isDeleting: boolean;
  onDelete: () => void;
  onTogglePin: () => void;
}) {
  const date = new Date(list.updatedAt).toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
  });

  const browseLink = `/profile/lists/${list.id}`;

  const browseLinkLabel = list.type === "ShoppingList"
    ? "View List"
    : list.type === "GearSet"
      ? "View Gear Set"
      : "View Plan";

  return (
    <div className="flex items-center gap-3 p-3 bg-bg-surface rounded-lg border border-border">
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          {list.isPinned && <Pin size={12} className="text-gold shrink-0" />}
          <span className="text-sm font-medium text-text-primary truncate">{list.name}</span>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-xs text-text-muted">{date}</span>
          <Link
            href={browseLink}
            className="inline-flex items-center gap-1 text-xs text-gold/70 hover:text-gold transition-colors"
          >
            <ExternalLink size={10} />
            {browseLinkLabel}
          </Link>
        </div>
      </div>
      <div className="flex items-center gap-1 shrink-0">
        <button
          onClick={onTogglePin}
          className={`p-1.5 rounded transition-colors ${
            list.isPinned ? "text-gold" : "text-text-muted hover:text-text-secondary"
          }`}
          aria-label={list.isPinned ? "Unpin" : "Pin"}
        >
          <Pin size={14} />
        </button>
        <button
          onClick={onDelete}
          disabled={isDeleting}
          className="p-1.5 rounded text-text-muted hover:text-red-400 transition-colors disabled:opacity-50"
          aria-label="Delete"
        >
          {isDeleting ? <LoadingSpinner size={14} /> : <Trash2 size={14} />}
        </button>
      </div>
    </div>
  );
}

function FavoritesTabContent() {
  const { favorites, isLoading, error, toggleFavorite } = useFavorites();
  const [togglingId, setTogglingId] = useState<number | null>(null);

  async function handleUnfavorite(recipeId: number) {
    setTogglingId(recipeId);
    try {
      await toggleFavorite(recipeId);
    } finally {
      setTogglingId(null);
    }
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-8">
        <LoadingSpinner size={24} />
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-3 rounded-lg bg-red-500/10 border border-red-500/30 text-red-400 text-sm text-center">
        {error}
      </div>
    );
  }

  if (favorites.length === 0) {
    return (
      <div className="text-center py-8 text-text-muted text-sm">
        <Heart className="mx-auto mb-2 opacity-40" size={24} />
        No favorite recipes yet. Browse recipes and tap the heart to save them.
      </div>
    );
  }

  return (
    <div className="space-y-2">
      {favorites.map((fav) => (
        <div
          key={fav.recipeId}
          className="flex items-center gap-3 p-3 bg-bg-surface rounded-lg border border-border hover:border-border-focus transition-colors"
        >
          <ItemIcon iconUrl={fav.iconUrl} alt={fav.itemName} size="lg" />
          <div className="flex-1 min-w-0">
            <Link
              href={recipePath(fav.recipeId, fav.itemName)}
              className="text-sm font-medium text-text-primary hover:text-gold transition-colors truncate block"
            >
              {fav.itemName}
            </Link>
            <div className="flex items-center gap-2 text-xs text-text-muted">
              <span>{fav.craftType}</span>
              <span>Lv. {fav.recipeLevel}</span>
            </div>
          </div>
          <Link
            href={recipeJourneyPath(fav.recipeId, fav.itemName)}
            className="p-1.5 rounded text-text-muted hover:text-gold transition-colors shrink-0"
            aria-label="Start journey"
          >
            <Map size={14} />
          </Link>
          <button
            onClick={() => handleUnfavorite(fav.recipeId)}
            disabled={togglingId === fav.recipeId}
            className="p-1.5 rounded text-red-400 hover:text-red-300 transition-colors shrink-0 disabled:opacity-50"
            aria-label="Remove from favorites"
          >
            {togglingId === fav.recipeId ? (
              <LoadingSpinner size={14} />
            ) : (
              <Heart size={14} fill="currentColor" />
            )}
          </button>
        </div>
      ))}
    </div>
  );
}

function EmptyListState({ type }: { type: string }) {
  const messages: Record<string, string> = {
    ShoppingList: "No shopping lists yet. Save a shopping list from any recipe!",
    GearSet: "No gear sets saved yet. Save a gear set from the gear page!",
    CraftingPlan: "No crafting plans yet. Create one to track multiple recipes!",
  };

  const icons: Record<string, typeof ShoppingCart> = {
    ShoppingList: ShoppingCart,
    GearSet: Shield,
    CraftingPlan: ClipboardList,
  };

  const Icon = icons[type] ?? ShoppingCart;

  return (
    <div className="text-center py-8 text-text-muted text-sm">
      <Icon className="mx-auto mb-2 opacity-40" size={24} />
      {messages[type] ?? "No items yet."}
    </div>
  );
}
