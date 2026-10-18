"use client";

import { Search, X } from "lucide-react";
import { useRef, useEffect } from "react";
import { LoadingSpinner } from "./LoadingSpinner";

interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
  isLoading?: boolean;
  autoFocus?: boolean;
  placeholder?: string;
  /** The accessible name of the field; the visible hint is the placeholder. */
  label?: string;
}

export function SearchBar({
  value,
  onChange,
  isLoading = false,
  autoFocus = false,
  placeholder = "Search items, recipes, materials...",
  label = "Search items",
}: SearchBarProps) {
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (autoFocus) {
      inputRef.current?.focus();
    }
  }, [autoFocus]);

  return (
    <div className="relative">
      <div className="absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none">
        {isLoading ? (
          <LoadingSpinner size={18} />
        ) : (
          <Search size={18} className="text-text-muted" />
        )}
      </div>
      <input
        ref={inputRef}
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        className="w-full h-11 pl-10 pr-10 bg-bg-secondary text-text-primary placeholder:text-text-muted rounded-lg border border-border focus:border-border-focus focus:outline-none transition-colors"
        aria-label={label}
      />
      {value && (
        <button
          onClick={() => onChange("")}
          className="absolute right-3 top-1/2 -translate-y-1/2 text-text-muted hover:text-text-primary transition-colors"
          aria-label="Clear search"
        >
          <X size={18} />
        </button>
      )}
    </div>
  );
}
