import { Wrench, TreePine } from "lucide-react";
import type { ProfileStatsDto } from "@/types/profile";

interface ProfileStatsProps {
  stats: ProfileStatsDto;
}

function StatValue({ label, value }: { label: string; value: number }) {
  return (
    <div className="text-center">
      <div className="text-xs text-text-muted mb-0.5">{label}</div>
      <div className="text-xl font-bold text-text-primary">{value.toLocaleString()}</div>
    </div>
  );
}

export function ProfileStats({ stats }: ProfileStatsProps) {
  const hasCraftingStats = stats.craftsmanship > 0 || stats.control > 0 || stats.cp > 0;
  const hasGatheringStats = stats.gathering > 0 || stats.perception > 0 || stats.gp > 0;

  return (
    <div className="space-y-3">
      {hasCraftingStats && (
        <div className="p-4 bg-bg-surface rounded-lg border border-border">
          <div className="flex items-center gap-2 mb-3">
            <Wrench className="text-gold" size={16} />
            <h3 className="text-sm font-semibold text-text-primary">Crafting Stats</h3>
            {stats.isFromGear && (
              <span className="text-xs px-2 py-0.5 rounded-full bg-blue-500/10 text-blue-400">
                From Gear
              </span>
            )}
          </div>
          <div className="grid grid-cols-3 gap-4">
            <StatValue label="Craftsmanship" value={stats.craftsmanship} />
            <StatValue label="Control" value={stats.control} />
            <StatValue label="CP" value={stats.cp} />
          </div>
        </div>
      )}

      {hasGatheringStats && (
        <div className="p-4 bg-bg-surface rounded-lg border border-border">
          <div className="flex items-center gap-2 mb-3">
            <TreePine className="text-emerald-400" size={16} />
            <h3 className="text-sm font-semibold text-text-primary">Gathering Stats</h3>
            {stats.isFromGear && (
              <span className="text-xs px-2 py-0.5 rounded-full bg-blue-500/10 text-blue-400">
                From Gear
              </span>
            )}
          </div>
          <div className="grid grid-cols-3 gap-4">
            <StatValue label="Gathering" value={stats.gathering} />
            <StatValue label="Perception" value={stats.perception} />
            <StatValue label="GP" value={stats.gp} />
          </div>
        </div>
      )}

      {!hasCraftingStats && !hasGatheringStats && (
        <div className="p-4 bg-bg-surface rounded-lg border border-border text-center text-text-muted text-sm">
          No stats recorded yet. Set up your profile to see stats here.
        </div>
      )}
    </div>
  );
}
