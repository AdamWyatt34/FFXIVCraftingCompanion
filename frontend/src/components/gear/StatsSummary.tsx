import type { GearStatsDto } from "@/types/gear";

interface StatsSummaryProps {
  stats: GearStatsDto;
  isDoH: boolean;
  profileName?: string;
}

export function StatsSummary({ stats, isDoH, profileName }: StatsSummaryProps) {
  const statEntries = isDoH
    ? [
        { label: "Craftsmanship", value: stats.craftsmanship },
        { label: "Control", value: stats.control },
        { label: "CP", value: stats.cp },
      ]
    : [
        { label: "Gathering", value: stats.gathering },
        { label: "Perception", value: stats.perception },
        { label: "GP", value: stats.gp },
      ];

  return (
    <div className="rounded-lg border border-gold/30 bg-bg-surface p-4">
      {/* Header */}
      <div className="flex items-center justify-between mb-3">
        <p className="text-xs uppercase tracking-wider text-text-muted">
          Total Stats
        </p>
        {profileName && (
          <span className="text-xs px-2 py-0.5 rounded-full bg-gold/10 text-gold font-medium">
            {profileName}
          </span>
        )}
      </div>

      {/* Stats grid */}
      <div className="grid grid-cols-3 gap-4">
        {statEntries.map(({ label, value }) => (
          <div key={label} className="text-center">
            <p className="text-lg font-bold text-text-primary">
              {value.toLocaleString()}
            </p>
            <p className="text-xs text-text-muted">{label}</p>
          </div>
        ))}
      </div>

      {/* Weighted score */}
      <div className="mt-3 pt-3 border-t border-border flex justify-between items-center">
        <span className="text-text-secondary text-sm">Weighted Score</span>
        <span className="text-gold font-bold text-lg">
          {stats.totalWeightedScore.toLocaleString(undefined, {
            maximumFractionDigits: 1,
          })}
        </span>
      </div>
    </div>
  );
}
