"use client";

import { useWorldList } from "@/hooks/useMarket";

interface WorldSelectorProps {
  value: string;
  onChange: (world: string) => void;
  disabled?: boolean;
  showDcOption?: boolean;
  className?: string;
}

export function WorldSelector({
  value,
  onChange,
  disabled,
  showDcOption = true,
  className,
}: WorldSelectorProps) {
  const { worlds, isLoading } = useWorldList();

  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className={`bg-bg-elevated border border-border rounded-md px-3 py-2 text-sm text-text-primary ${className ?? ""}`}
      disabled={disabled || isLoading}
    >
      <option value="">Select world/DC...</option>
      {worlds.map((region) =>
        region.dataCenters.map((dc) => (
          <optgroup key={dc.name} label={`${region.region} - ${dc.name}`}>
            {showDcOption && (
              <option value={dc.name}>{dc.name} (DC)</option>
            )}
            {dc.worlds.map((w) => (
              <option key={w} value={w}>{w}</option>
            ))}
          </optgroup>
        )),
      )}
    </select>
  );
}
