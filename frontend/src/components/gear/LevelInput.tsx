"use client";

import { LEVEL_PRESETS } from "@/lib/gear-constants";

interface LevelInputProps {
  value: number;
  onChange: (level: number) => void;
}

export function LevelInput({ value, onChange }: LevelInputProps) {
  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const raw = parseInt(e.target.value, 10);
    if (!isNaN(raw)) {
      onChange(Math.max(1, Math.min(100, raw)));
    }
  };

  const handleBlur = () => {
    if (value < 1) onChange(1);
    if (value > 100) onChange(100);
  };

  return (
    <div className="space-y-2">
      <label htmlFor="level-input" className="text-sm font-medium text-text-secondary">
        Your Level
      </label>
      <div className="flex items-center gap-3">
        <input
          id="level-input"
          type="number"
          min={1}
          max={100}
          value={value}
          onChange={handleChange}
          onBlur={handleBlur}
          className="w-20 px-3 py-2 rounded-lg bg-bg-secondary border border-border focus:border-border-focus focus:outline-none text-text-primary text-center text-lg font-bold"
        />
        <div className="flex flex-wrap gap-1.5">
          {LEVEL_PRESETS.map((preset) => (
            <button
              key={preset}
              onClick={() => onChange(preset)}
              className={`px-3 py-1.5 min-h-[44px] rounded-md text-sm font-medium transition-colors ${
                value === preset
                  ? "bg-gold text-bg-primary"
                  : "bg-bg-surface border border-border text-text-secondary hover:border-border-focus"
              }`}
            >
              {preset}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
