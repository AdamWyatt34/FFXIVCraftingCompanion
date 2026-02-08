"use client";

import { useState, useRef, useEffect } from "react";
import { Copy, ChevronDown, ClipboardList, FileText, CheckSquare, X } from "lucide-react";
import { useToast } from "@/components/ui";
import { formatShoppingListText } from "@/lib/tree-utils";
import type { ShoppingListItemDto, IntermediateCraftDto } from "@/types/api";

type CopyFormat = "detailed" | "compact" | "checkbox";

const FORMAT_OPTIONS: {
  key: CopyFormat;
  label: string;
  description: string;
  icon: typeof Copy;
}[] = [
  {
    key: "detailed",
    label: "Detailed",
    description: "Full list with sources",
    icon: ClipboardList,
  },
  {
    key: "compact",
    label: "Compact",
    description: "Names and quantities only",
    icon: FileText,
  },
  {
    key: "checkbox",
    label: "With Checkboxes",
    description: "For notes apps",
    icon: CheckSquare,
  },
];

interface CopyShoppingListProps {
  shoppingList: ShoppingListItemDto[];
  crystalRequirements: ShoppingListItemDto[];
  intermediateCrafts: IntermediateCraftDto[];
  itemName: string;
  quantity: number;
}

export function CopyShoppingList({
  shoppingList,
  crystalRequirements,
  intermediateCrafts,
  itemName,
  quantity,
}: CopyShoppingListProps) {
  const { toast } = useToast();
  const [showDropdown, setShowDropdown] = useState(false);
  const [fallbackText, setFallbackText] = useState<string | null>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (
        dropdownRef.current &&
        !dropdownRef.current.contains(e.target as Node)
      ) {
        setShowDropdown(false);
      }
    }
    if (showDropdown) {
      document.addEventListener("mousedown", handleClickOutside);
      return () => document.removeEventListener("mousedown", handleClickOutside);
    }
  }, [showDropdown]);

  async function copyToClipboard(format: CopyFormat) {
    const text = formatShoppingListText(
      format,
      itemName,
      quantity,
      crystalRequirements,
      shoppingList,
      intermediateCrafts,
    );
    setShowDropdown(false);

    try {
      await navigator.clipboard.writeText(text);
      toast("Shopping list copied!", "success");
    } catch {
      setFallbackText(text);
    }
  }

  return (
    <>
      <div className="relative" ref={dropdownRef}>
        <div className="flex">
          <button
            onClick={() => copyToClipboard("detailed")}
            className="flex items-center gap-1.5 px-4 py-2 bg-gold text-bg-primary text-sm font-semibold rounded-l-lg hover:bg-gold-light transition-colors"
          >
            <Copy size={14} />
            Copy List
          </button>
          <button
            onClick={() => setShowDropdown((v) => !v)}
            className="flex items-center px-2 py-2 bg-gold text-bg-primary rounded-r-lg border-l border-gold-dark hover:bg-gold-light transition-colors"
            aria-label="Copy format options"
          >
            <ChevronDown size={14} />
          </button>
        </div>

        {showDropdown && (
          <div className="absolute bottom-full mb-1 right-0 w-56 bg-bg-elevated border border-border rounded-lg shadow-xl overflow-hidden z-10">
            {FORMAT_OPTIONS.map((opt) => {
              const Icon = opt.icon;
              return (
                <button
                  key={opt.key}
                  onClick={() => copyToClipboard(opt.key)}
                  className="w-full flex items-center gap-3 px-3 py-2.5 text-left hover:bg-bg-tertiary transition-colors"
                >
                  <Icon size={16} className="text-text-muted shrink-0" />
                  <div>
                    <div className="text-sm text-text-primary">{opt.label}</div>
                    <div className="text-xs text-text-muted">
                      {opt.description}
                    </div>
                  </div>
                </button>
              );
            })}
          </div>
        )}
      </div>

      {/* Fallback modal */}
      {fallbackText && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
          <div className="bg-bg-surface border border-border rounded-lg shadow-xl max-w-lg w-full max-h-[80vh] flex flex-col">
            <div className="flex items-center justify-between px-4 py-3 border-b border-border">
              <h3 className="text-sm font-semibold text-text-primary">
                Copy Shopping List
              </h3>
              <button
                onClick={() => setFallbackText(null)}
                className="text-text-muted hover:text-text-secondary transition-colors"
              >
                <X size={16} />
              </button>
            </div>
            <div className="p-4 overflow-auto flex-1">
              <p className="text-xs text-text-muted mb-2">
                Select all the text below and copy it manually (Ctrl+C / Cmd+C):
              </p>
              <textarea
                readOnly
                value={fallbackText}
                onFocus={(e) => e.target.select()}
                className="w-full h-64 bg-bg-secondary text-text-primary text-xs font-mono p-3 rounded-lg border border-border resize-none focus:outline-none focus:border-gold"
              />
            </div>
          </div>
        </div>
      )}
    </>
  );
}
