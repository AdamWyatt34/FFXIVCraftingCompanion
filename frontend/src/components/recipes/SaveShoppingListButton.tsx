"use client";

import { useState, useRef, useEffect } from "react";
import { Bookmark, Loader2 } from "lucide-react";
import { ProtectedAction } from "@/components/ui/ProtectedAction";
import { useToast } from "@/components/ui";
import { saveShoppingList, ApiClientError } from "@/lib/api-client";

interface SaveShoppingListButtonProps {
  recipeId: number;
  itemName: string;
  quantity: number;
}

export function SaveShoppingListButton({
  recipeId,
  itemName,
  quantity,
}: SaveShoppingListButtonProps) {
  const { toast } = useToast();
  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState(`Materials for ${quantity}x ${itemName}`);
  const [notes, setNotes] = useState("");
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const formRef = useRef<HTMLDivElement>(null);

  // Update default name when quantity/itemName changes
  useEffect(() => {
    if (!showForm) {
      setName(`Materials for ${quantity}x ${itemName}`);
    }
  }, [quantity, itemName, showForm]);

  // Close on outside click
  useEffect(() => {
    if (!showForm) return;
    function handleClickOutside(e: MouseEvent) {
      if (formRef.current && !formRef.current.contains(e.target as Node)) {
        setShowForm(false);
        setError(null);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [showForm]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) return;

    setIsSaving(true);
    setError(null);

    try {
      await saveShoppingList({
        name: name.trim(),
        recipeId,
        quantity,
        notes: notes.trim() || undefined,
      });
      toast("Shopping list saved!", "success");
      setShowForm(false);
      setNotes("");
    } catch (err) {
      setError(
        err instanceof ApiClientError
          ? err.body.error
          : "Failed to save shopping list",
      );
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <ProtectedAction message="Sign in to save shopping lists">
      <div className="relative" ref={formRef}>
        <button
          onClick={() => {
            setShowForm((v) => !v);
            setError(null);
          }}
          className="flex items-center gap-1.5 px-3 py-2 bg-bg-elevated text-text-secondary text-sm font-medium rounded-lg border border-border hover:border-border-focus hover:text-text-primary transition-colors"
          aria-label="Save shopping list"
        >
          <Bookmark size={14} />
          Save
        </button>

        {showForm && (
          <div className="absolute bottom-full mb-2 right-0 w-72 bg-bg-elevated border border-border rounded-lg shadow-xl z-10 p-3">
            <form onSubmit={handleSubmit} className="space-y-3">
              <div>
                <label
                  htmlFor="save-list-name"
                  className="block text-xs font-medium text-text-secondary mb-1"
                >
                  Name
                </label>
                <input
                  id="save-list-name"
                  type="text"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  className="w-full px-2.5 py-1.5 text-sm bg-bg-primary text-text-primary border border-border rounded-md focus:outline-none focus:border-gold"
                  required
                  autoFocus
                />
              </div>
              <div>
                <label
                  htmlFor="save-list-notes"
                  className="block text-xs font-medium text-text-secondary mb-1"
                >
                  Notes (optional)
                </label>
                <textarea
                  id="save-list-notes"
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  rows={3}
                  className="w-full px-2.5 py-1.5 text-sm bg-bg-primary text-text-primary border border-border rounded-md focus:outline-none focus:border-gold resize-none"
                />
              </div>
              {error && (
                <p className="text-xs text-red-400">{error}</p>
              )}
              <button
                type="submit"
                disabled={isSaving || !name.trim()}
                className="w-full flex items-center justify-center gap-1.5 px-3 py-2 bg-gold text-bg-primary text-sm font-semibold rounded-md hover:bg-gold-light transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isSaving ? (
                  <>
                    <Loader2 size={14} className="animate-spin" />
                    Saving...
                  </>
                ) : (
                  "Save Shopping List"
                )}
              </button>
            </form>
          </div>
        )}
      </div>
    </ProtectedAction>
  );
}
