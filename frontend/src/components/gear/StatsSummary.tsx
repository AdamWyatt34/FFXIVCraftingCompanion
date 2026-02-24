import type { GearStatsDto } from "@/types/gear";
import type { JobCategory } from "@/lib/gear-constants";
import { isCombatJob } from "@/lib/gear-constants";

interface StatsSummaryProps {
  stats: GearStatsDto;
  isDoH: boolean;
  jobCategory?: JobCategory;
  profileName?: string;
}

export function StatsSummary({ stats, isDoH, jobCategory, profileName }: StatsSummaryProps) {
  const isCombat = jobCategory ? isCombatJob(jobCategory) : false;

  let statEntries: { label: string; value: number }[];

  if (isCombat) {
    // Show primary combat stats + most relevant secondary stats
    statEntries = [
      { label: "Weapon DMG", value: stats.physicalDamage },
      { label: "Vitality", value: stats.vitality },
      ...(jobCategory === "Tank"
        ? [
            { label: "Strength", value: stats.strength },
            { label: "Defense", value: stats.defense },
            { label: "Crit", value: stats.criticalHit },
            { label: "Det", value: stats.determination },
          ]
        : jobCategory === "Healer"
          ? [
              { label: "Mind", value: stats.mind },
              { label: "Crit", value: stats.criticalHit },
              { label: "Det", value: stats.determination },
              { label: "Piety", value: stats.piety },
            ]
          : jobCategory === "MagicDPS"
            ? [
                { label: "Intelligence", value: stats.intelligence },
                { label: "Crit", value: stats.criticalHit },
                { label: "Direct Hit", value: stats.directHitRate },
                { label: "Det", value: stats.determination },
              ]
            : [
                { label: "STR/DEX", value: Math.max(stats.strength, stats.dexterity) },
                { label: "Crit", value: stats.criticalHit },
                { label: "Direct Hit", value: stats.directHitRate },
                { label: "Det", value: stats.determination },
              ]),
    ];
  } else if (isDoH) {
    statEntries = [
      { label: "Craftsmanship", value: stats.craftsmanship },
      { label: "Control", value: stats.control },
      { label: "CP", value: stats.cp },
    ];
  } else {
    statEntries = [
      { label: "Gathering", value: stats.gathering },
      { label: "Perception", value: stats.perception },
      { label: "GP", value: stats.gp },
    ];
  }

  const gridCols = statEntries.length <= 3 ? "grid-cols-3" : "grid-cols-3 sm:grid-cols-6";

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
      <div className={`grid ${gridCols} gap-4`}>
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
