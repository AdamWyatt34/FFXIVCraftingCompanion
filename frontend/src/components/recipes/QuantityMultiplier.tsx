"use client";

import { useState } from "react";
import { Minus, Plus } from "lucide-react";

interface QuantityMultiplierProps {
  value: number;
  onChange: (quantity: number) => void;
}

const QUICK_VALUES = [1, 3, 5, 10, 99];

function clamp(n: number): number {
  return Math.max(1, Math.min(999, n));
}

export function QuantityMultiplier({
  value,
  onChange,
}: QuantityMultiplierProps) {
  const [inputValue, setInputValue] = useState(String(value));
  const [isFocused, setIsFocused] = useState(false);

  const handleIncrement = () => {
    const next = clamp(value + 1);
    onChange(next);
    setInputValue(String(next));
  };

  const handleDecrement = () => {
    const next = clamp(value - 1);
    onChange(next);
    setInputValue(String(next));
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const raw = e.target.value.replace(/\D/g, "");
    setInputValue(raw);
  };

  const handleInputBlur = () => {
    setIsFocused(false);
    const parsed = parseInt(inputValue, 10);
    const clamped = isNaN(parsed) ? 1 : clamp(parsed);
    onChange(clamped);
    setInputValue(String(clamped));
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter") {
      (e.target as HTMLInputElement).blur();
    }
  };

  const displayValue = isFocused ? inputValue : String(value);

  return (
    <div className="bg-bg-surface rounded-lg p-4 border border-border">
      <label className="text-sm font-semibold text-text-secondary uppercase tracking-wider block mb-3">
        How many do you want to craft?
      </label>
      <div className="flex items-center gap-3 flex-wrap">
        <div className="flex items-center gap-1">
          <button
            onClick={handleDecrement}
            disabled={value <= 1}
            className="w-11 h-11 flex items-center justify-center rounded-lg bg-bg-elevated text-text-secondary hover:text-gold hover:bg-bg-tertiary disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
            aria-label="Decrease quantity"
          >
            <Minus size={18} />
          </button>
          <input
            type="text"
            inputMode="numeric"
            value={displayValue}
            onChange={handleInputChange}
            onFocus={() => setIsFocused(true)}
            onBlur={handleInputBlur}
            onKeyDown={handleKeyDown}
            className="w-16 h-11 text-center bg-bg-elevated text-text-primary rounded-lg border border-border focus:border-gold focus:outline-none font-mono text-lg"
            aria-label="Craft quantity"
          />
          <button
            onClick={handleIncrement}
            disabled={value >= 999}
            className="w-11 h-11 flex items-center justify-center rounded-lg bg-bg-elevated text-text-secondary hover:text-gold hover:bg-bg-tertiary disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
            aria-label="Increase quantity"
          >
            <Plus size={18} />
          </button>
        </div>

        <div className="flex flex-wrap gap-1.5">
          {QUICK_VALUES.map((q) => (
            <button
              key={q}
              onClick={() => {
                onChange(q);
                setInputValue(String(q));
              }}
              className={`px-3 h-9 rounded-full text-sm font-medium transition-colors ${
                value === q
                  ? "bg-gold text-bg-primary"
                  : "bg-bg-elevated text-text-secondary hover:text-gold hover:bg-bg-tertiary"
              }`}
            >
              x{q}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
