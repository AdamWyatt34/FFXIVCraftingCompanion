interface StatBarProps {
  label: string;
  value: number;
  required?: number;
}

export function StatBar({ label, value, required }: StatBarProps) {
  const meetsRequirement = required === undefined || value >= required;
  const percentage = required ? Math.min((value / required) * 100, 100) : 100;

  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between text-sm">
        <span className="text-text-secondary">{label}</span>
        <span className={meetsRequirement ? "text-rarity-uncommon" : "text-red-400"}>
          {/* A fixed locale: this renders on the server too, and a visitor's locale would mismatch. */}
          {value.toLocaleString("en-US")}
          {required !== undefined && (
            <>
              <span className="text-text-muted"> / {required.toLocaleString("en-US")}</span>
              <span className="sr-only">{meetsRequirement ? " (requirement met)" : " (below requirement)"}</span>
            </>
          )}
        </span>
      </div>
      {required !== undefined && (
        <div className="h-1.5 bg-bg-elevated rounded-full overflow-hidden">
          <div
            className={`h-full rounded-full transition-all ${
              meetsRequirement ? "bg-rarity-uncommon" : "bg-red-400"
            }`}
            style={{ width: `${percentage}%` }}
          />
        </div>
      )}
    </div>
  );
}
