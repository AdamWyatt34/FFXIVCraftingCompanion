"use client";

import { use, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import {
  ArrowLeft,
  Pin,
  Trash2,
  ShoppingCart,
  Shield,
  ClipboardList,
  Plus,
  X,
} from "lucide-react";
import { useSavedListDetail } from "@/hooks/useSavedListDetail";
import { ItemLink } from "@/components/ui/ItemLink";
import { LoadingSpinner } from "@/components/ui/LoadingSpinner";
import { recipePath } from "@/lib/routes";
import type {
  ShoppingListData,
  ShoppingListMaterial,
  GearSetData,
  CraftingPlanData,
} from "@/types/saved-lists";

const TYPE_META: Record<string, { label: string; icon: typeof ShoppingCart; color: string }> = {
  ShoppingList: { label: "Shopping List", icon: ShoppingCart, color: "text-blue-400" },
  GearSet: { label: "Gear Set", icon: Shield, color: "text-green-400" },
  CraftingPlan: { label: "Crafting Plan", icon: ClipboardList, color: "text-purple-400" },
};

export default function SavedListDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const listId = parseInt(id, 10);
  const router = useRouter();
  const { list, isLoading, error, togglePin, deleteList, addItem, removeItem } =
    useSavedListDetail(listId);
  const [isDeleting, setIsDeleting] = useState(false);
  const [showAddForm, setShowAddForm] = useState(false);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-16">
        <LoadingSpinner size={32} />
      </div>
    );
  }

  if (error || !list) {
    return (
      <div className="p-4 max-w-2xl mx-auto">
        <Link
          href="/profile"
          className="inline-flex items-center gap-1 text-sm text-text-muted hover:text-text-secondary transition-colors mb-4"
        >
          <ArrowLeft size={16} />
          Back to Profile
        </Link>
        <div className="p-4 rounded-lg bg-red-500/10 border border-red-500/30 text-red-400 text-sm text-center">
          {error ?? "List not found."}
        </div>
      </div>
    );
  }

  const meta = TYPE_META[list.type] ?? TYPE_META.ShoppingList;
  const TypeIcon = meta.icon;

  async function handleDelete() {
    setIsDeleting(true);
    const success = await deleteList();
    if (success) {
      router.push("/profile");
    } else {
      setIsDeleting(false);
    }
  }

  return (
    <div className="p-4 max-w-2xl mx-auto space-y-4">
      {/* Breadcrumbs */}
      <div className="flex items-center gap-1 text-sm text-text-muted">
        <Link href="/profile" className="hover:text-text-secondary transition-colors">
          Profile
        </Link>
        <span>/</span>
        <span className="text-text-secondary truncate">{list.name}</span>
      </div>

      {/* Header */}
      <div className="flex items-start gap-3">
        <div className={`w-10 h-10 rounded-lg bg-bg-surface flex items-center justify-center shrink-0 ${meta.color}`}>
          <TypeIcon size={20} />
        </div>
        <div className="flex-1 min-w-0">
          <h1 className="text-lg font-bold text-text-primary truncate">{list.name}</h1>
          <div className="flex items-center gap-2 text-xs text-text-muted mt-0.5">
            <span className={`px-2 py-0.5 rounded-full bg-bg-surface ${meta.color}`}>
              {meta.label}
            </span>
            <span>
              Updated {new Date(list.updatedAt).toLocaleDateString(undefined, {
                month: "short",
                day: "numeric",
                year: "numeric",
              })}
            </span>
          </div>
        </div>
        <div className="flex items-center gap-1 shrink-0">
          <button
            onClick={togglePin}
            className={`p-2 rounded-lg transition-colors ${
              list.isPinned ? "text-gold bg-gold/10" : "text-text-muted hover:text-text-secondary"
            }`}
            aria-label={list.isPinned ? "Unpin" : "Pin"}
          >
            <Pin size={16} />
          </button>
          <button
            onClick={handleDelete}
            disabled={isDeleting}
            className="p-2 rounded-lg text-text-muted hover:text-red-400 transition-colors disabled:opacity-50"
            aria-label="Delete list"
          >
            {isDeleting ? <LoadingSpinner size={16} /> : <Trash2 size={16} />}
          </button>
        </div>
      </div>

      {/* Content based on type */}
      {list.type === "ShoppingList" && (
        <ShoppingListContent
          data={list.data as ShoppingListData}
          onRemoveItem={removeItem}
          onAddItem={addItem}
          showAddForm={showAddForm}
          setShowAddForm={setShowAddForm}
        />
      )}
      {list.type === "GearSet" && <GearSetContent data={list.data as GearSetData} />}
      {list.type === "CraftingPlan" && <CraftingPlanContent data={list.data as CraftingPlanData} />}
    </div>
  );
}

function ShoppingListContent({
  data,
  onRemoveItem,
  onAddItem,
  showAddForm,
  setShowAddForm,
}: {
  data: ShoppingListData;
  onRemoveItem: (itemId: number) => Promise<void>;
  onAddItem: (itemId: number, itemName: string, quantity: number, category?: string) => Promise<void>;
  showAddForm: boolean;
  setShowAddForm: (show: boolean) => void;
}) {
  const [removingId, setRemovingId] = useState<number | null>(null);

  async function handleRemove(itemId: number) {
    setRemovingId(itemId);
    await onRemoveItem(itemId);
    setRemovingId(null);
  }

  return (
    <div className="space-y-4">
      {/* Source info */}
      <div className="p-3 rounded-lg bg-bg-surface border border-border">
        <div className="flex items-center justify-between">
          <div>
            <span className="text-sm text-text-secondary">Source: </span>
            {data.sourceRecipeId ? (
              <Link
                href={recipePath(data.sourceRecipeId, data.sourceItemName)}
                className="text-sm text-gold hover:text-gold-light transition-colors"
              >
                {data.sourceItemName}
              </Link>
            ) : (
              <span className="text-sm text-text-primary">{data.sourceItemName ?? "Custom"}</span>
            )}
          </div>
          {data.quantity > 1 && (
            <span className="text-xs text-text-muted">x{data.quantity}</span>
          )}
        </div>
        {data.notes && (
          <p className="text-xs text-text-muted mt-1">{data.notes}</p>
        )}
      </div>

      {/* Materials */}
      <div>
        <div className="flex items-center justify-between mb-2">
          <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
            Materials ({data.materials?.length ?? 0})
          </h2>
          <button
            onClick={() => setShowAddForm(!showAddForm)}
            className="flex items-center gap-1 text-xs text-gold hover:text-gold-light transition-colors"
          >
            <Plus size={14} />
            Add Item
          </button>
        </div>

        {showAddForm && (
          <AddItemForm
            onAdd={async (itemId, itemName, qty, category) => {
              await onAddItem(itemId, itemName, qty, category);
              setShowAddForm(false);
            }}
            onCancel={() => setShowAddForm(false)}
          />
        )}

        {data.materials && data.materials.length > 0 ? (
          <MaterialTable
            materials={data.materials}
            removingId={removingId}
            onRemove={handleRemove}
          />
        ) : (
          <p className="text-sm text-text-muted text-center py-4">No materials.</p>
        )}
      </div>

      {/* Crystals */}
      {data.crystals && data.crystals.length > 0 && (
        <div>
          <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider mb-2">
            Crystals ({data.crystals.length})
          </h2>
          <MaterialTable
            materials={data.crystals}
            removingId={removingId}
            onRemove={handleRemove}
          />
        </div>
      )}
    </div>
  );
}

function MaterialTable({
  materials,
  removingId,
  onRemove,
}: {
  materials: ShoppingListMaterial[];
  removingId: number | null;
  onRemove: (itemId: number) => void;
}) {
  return (
    <div className="space-y-1">
      {materials.map((mat) => (
        <div
          key={mat.itemId}
          className="flex items-center gap-3 p-2 rounded-lg bg-bg-surface border border-border hover:border-border-focus transition-colors"
        >
          <div className="flex-1 min-w-0">
            <ItemLink itemId={mat.itemId} itemName={mat.itemName} showIcon={false} />
          </div>
          <span className="text-xs text-text-muted px-2 py-0.5 rounded bg-bg-secondary">
            {mat.category}
          </span>
          <span className="text-sm font-medium text-text-primary tabular-nums w-12 text-right">
            x{mat.quantity}
          </span>
          <button
            onClick={() => onRemove(mat.itemId)}
            disabled={removingId === mat.itemId}
            className="p-1 rounded text-text-muted hover:text-red-400 transition-colors disabled:opacity-50 shrink-0"
            aria-label={`Remove ${mat.itemName}`}
          >
            {removingId === mat.itemId ? <LoadingSpinner size={14} /> : <X size={14} />}
          </button>
        </div>
      ))}
    </div>
  );
}

function AddItemForm({
  onAdd,
  onCancel,
}: {
  onAdd: (itemId: number, itemName: string, quantity: number, category?: string) => Promise<void>;
  onCancel: () => void;
}) {
  const [itemId, setItemId] = useState("");
  const [itemName, setItemName] = useState("");
  const [quantity, setQuantity] = useState("1");
  const [category, setCategory] = useState("Other");
  const [isAdding, setIsAdding] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const id = parseInt(itemId, 10);
    const qty = parseInt(quantity, 10);
    if (!id || !itemName.trim() || qty < 1) return;

    setIsAdding(true);
    await onAdd(id, itemName.trim(), qty, category || undefined);
    setIsAdding(false);
  }

  return (
    <form onSubmit={handleSubmit} className="p-3 mb-2 rounded-lg bg-bg-surface border border-gold/30 space-y-2">
      <div className="grid grid-cols-2 gap-2">
        <input
          type="number"
          placeholder="Item ID"
          value={itemId}
          onChange={(e) => setItemId(e.target.value)}
          className="h-9 px-3 text-sm bg-bg-secondary border border-border rounded-lg text-text-primary placeholder:text-text-muted focus:outline-none focus:border-border-focus"
          required
          min={1}
        />
        <input
          type="text"
          placeholder="Item Name"
          value={itemName}
          onChange={(e) => setItemName(e.target.value)}
          className="h-9 px-3 text-sm bg-bg-secondary border border-border rounded-lg text-text-primary placeholder:text-text-muted focus:outline-none focus:border-border-focus"
          required
        />
      </div>
      <div className="grid grid-cols-2 gap-2">
        <input
          type="number"
          placeholder="Quantity"
          value={quantity}
          onChange={(e) => setQuantity(e.target.value)}
          className="h-9 px-3 text-sm bg-bg-secondary border border-border rounded-lg text-text-primary placeholder:text-text-muted focus:outline-none focus:border-border-focus"
          required
          min={1}
        />
        <select
          value={category}
          onChange={(e) => setCategory(e.target.value)}
          className="h-9 px-3 text-sm bg-bg-secondary border border-border rounded-lg text-text-primary focus:outline-none focus:border-border-focus"
        >
          <option value="Gathered">Gathered</option>
          <option value="Crafted">Crafted</option>
          <option value="Vendor">Vendor</option>
          <option value="Crystal">Crystal</option>
          <option value="Other">Other</option>
        </select>
      </div>
      <div className="flex gap-2">
        <button
          type="submit"
          disabled={isAdding}
          className="flex-1 h-9 bg-gold text-bg-primary text-sm font-medium rounded-lg hover:bg-gold-light disabled:opacity-50 transition-colors flex items-center justify-center gap-1"
        >
          {isAdding ? <LoadingSpinner size={14} /> : <Plus size={14} />}
          Add
        </button>
        <button
          type="button"
          onClick={onCancel}
          className="h-9 px-4 text-sm text-text-muted border border-border rounded-lg hover:text-text-secondary transition-colors"
        >
          Cancel
        </button>
      </div>
    </form>
  );
}

function GearSetContent({ data }: { data: GearSetData }) {
  return (
    <div className="space-y-4">
      <div className="p-3 rounded-lg bg-bg-surface border border-border">
        <span className="text-sm text-text-secondary">Job: </span>
        <span className="text-sm font-medium text-text-primary">{data.job}</span>
        {data.level && (
          <>
            <span className="text-sm text-text-muted mx-2">|</span>
            <span className="text-sm text-text-muted">Lv. {data.level}</span>
          </>
        )}
      </div>

      {data.stats && (
        <div>
          <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider mb-2">
            Stats
          </h2>
          <div className="grid grid-cols-3 gap-2">
            {Object.entries(data.stats)
              .filter(([, v]) => v && v > 0)
              .map(([stat, value]) => (
                <div key={stat} className="p-2 rounded-lg bg-bg-surface border border-border text-center">
                  <div className="text-xs text-text-muted capitalize">{stat}</div>
                  <div className="text-sm font-medium text-text-primary">{value}</div>
                </div>
              ))}
          </div>
        </div>
      )}

      <div>
        <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider mb-2">
          Equipment Slots ({Object.keys(data.slots).length})
        </h2>
        <div className="space-y-1">
          {Object.entries(data.slots).map(([slot, equipId]) => (
            <div
              key={slot}
              className="flex items-center justify-between p-2 rounded-lg bg-bg-surface border border-border"
            >
              <span className="text-sm text-text-secondary">{slot}</span>
              <span className="text-xs text-text-muted">ID: {equipId}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function CraftingPlanContent({ data }: { data: CraftingPlanData }) {
  return (
    <div className="space-y-4">
      <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
        Recipes ({data.items?.length ?? 0})
      </h2>
      {data.items && data.items.length > 0 ? (
        <div className="space-y-1">
          {data.items.map((item, i) => (
            <div
              key={i}
              className="flex items-center justify-between p-2 rounded-lg bg-bg-surface border border-border"
            >
              <Link
                href={recipePath(item.recipeId)}
                className="text-sm text-gold hover:text-gold-light transition-colors"
              >
                Recipe #{item.recipeId}
              </Link>
              <span className="text-sm text-text-muted">x{item.quantity}</span>
            </div>
          ))}
        </div>
      ) : (
        <p className="text-sm text-text-muted text-center py-4">No recipes in this plan.</p>
      )}
    </div>
  );
}
