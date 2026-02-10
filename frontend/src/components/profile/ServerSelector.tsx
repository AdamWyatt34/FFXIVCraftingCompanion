"use client";

import { FFXIV_REGIONS } from "@/lib/server-data";

interface ServerSelectorProps {
  value: string;
  onChange: (server: string) => void;
  id?: string;
  required?: boolean;
}

export function ServerSelector({ value, onChange, id, required }: ServerSelectorProps) {
  return (
    <select
      id={id}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      required={required}
      className="w-full h-11 px-3 bg-bg-secondary border border-border rounded-lg text-text-primary focus:outline-none focus:border-border-focus transition-colors appearance-none cursor-pointer"
      style={{
        backgroundImage: `url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 20 20'%3e%3cpath stroke='%236b6b80' stroke-linecap='round' stroke-linejoin='round' stroke-width='1.5' d='M6 8l4 4 4-4'/%3e%3c/svg%3e")`,
        backgroundPosition: "right 0.5rem center",
        backgroundRepeat: "no-repeat",
        backgroundSize: "1.5em 1.5em",
        paddingRight: "2.5rem",
      }}
    >
      <option value="">Select a server...</option>
      {FFXIV_REGIONS.map((region) =>
        region.dataCenters.map((dc) => (
          <optgroup key={dc.name} label={`${region.region} - ${dc.name}`}>
            {dc.servers.map((server) => (
              <option key={server} value={server}>
                {server}
              </option>
            ))}
          </optgroup>
        )),
      )}
    </select>
  );
}
