"use client";

import { TrendingUp } from "lucide-react";
import { ProtectedAction } from "@/components/ui/ProtectedAction";
import type { UpgradeSummaryDto } from "@/types/gear-comparison";

interface UpgradeSummaryBannerProps {
  summary: UpgradeSummaryDto;
  isDoH: boolean;
  onApplyAll: () => void;
  isAuthenticated: boolean;
}

export function UpgradeSummaryBanner({
  summary,
  isDoH,
  onApplyAll,
  isAuthenticated,
}: UpgradeSummaryBannerProps) {
  if (summary.upgradeCount === 0) return null;

  const diffs = summary.totalStatDifference;
  const isCombat = (diffs.vitality ?? 0) > 0 || (diffs.physicalDamage ?? 0) > 0;
  const statGains = isDoH
    ? [
        { label: "Craftsmanship", value: diffs.craftsmanship },
        { label: "Control", value: diffs.control },
        { label: "CP", value: diffs.cp },
      ]
    : isCombat
      ? [
          { label: "Weapon DMG", value: diffs.physicalDamage ?? 0 },
          { label: "Vitality", value: diffs.vitality ?? 0 },
          { label: "Crit", value: diffs.criticalHit ?? 0 },
          { label: "Det", value: diffs.determination ?? 0 },
          { label: "Direct Hit", value: diffs.directHitRate ?? 0 },
        ]
      : [
          { label: "Gathering", value: diffs.gathering },
          { label: "Perception", value: diffs.perception },
          { label: "GP", value: diffs.gp },
        ];

  const nonZeroGains = statGains.filter((s) => s.value !== 0);

  return (
    <div className="rounded-lg border border-emerald-500/20 bg-emerald-500/5 p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <div className="flex items-start gap-3">
        <span title="Upgrades available">
          <TrendingUp size={20} className="text-emerald-400 shrink-0 mt-0.5" />
        </span>
        <div>
          <p className="text-sm font-medium text-emerald-400">
            You have {summary.upgradeCount} gear upgrade
            {summary.upgradeCount > 1 ? "s" : ""} available
          </p>
          {nonZeroGains.length > 0 && (
            <div className="flex flex-wrap gap-x-3 gap-y-1 mt-1">
              {nonZeroGains.map((stat) => (
                <span key={stat.label} className="text-xs font-medium text-emerald-400">
                  +{stat.value.toLocaleString()} {stat.label}
                </span>
              ))}
            </div>
          )}
        </div>
      </div>
      {isAuthenticated && (
        <ProtectedAction>
          <button
            onClick={onApplyAll}
            className="shrink-0 px-4 py-2 rounded-lg bg-emerald-500/20 text-emerald-400 text-sm font-medium hover:bg-emerald-500/30 transition-colors"
          >
            Apply all upgrades to profile
          </button>
        </ProtectedAction>
      )}
    </div>
  );
}
