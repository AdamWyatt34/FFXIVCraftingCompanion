"use client";

import Link from "next/link";
import { CheckCircle, AlertTriangle, XCircle, FlaskConical, Sparkles } from "lucide-react";
import { StatBar } from "@/components/ui";
import { RECOMMENDATION_INFO } from "@/lib/constants";
import type { StatSufficiencyResultDto } from "@/types/journey";

interface StatCheckProps {
  sufficiency: StatSufficiencyResultDto;
  recipeId: number;
}

export function StatCheck({ sufficiency, recipeId }: StatCheckProps) {
  const rec = RECOMMENDATION_INFO[sufficiency.recommendation] ?? RECOMMENDATION_INFO.StatsAdequate;

  const Icon = sufficiency.meetsMinimumRequirements
    ? sufficiency.recommendation === "StatsExcellent"
      ? CheckCircle
      : AlertTriangle
    : XCircle;

  return (
    <div className={`rounded-lg border p-4 space-y-4 ${rec.bgClass}`}>
      <div className="flex items-center gap-2">
        <Icon size={18} className={rec.colorClass} />
        <span className={`text-sm font-semibold ${rec.colorClass}`}>
          {rec.label}
        </span>
        {sufficiency.canComplete && (
          <span className="text-xs text-text-muted ml-auto">
            ~{sufficiency.estimatedHQPercent}% HQ
          </span>
        )}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        {sufficiency.craftsmanshipDeficit !== 0 && (
          <StatBar
            label="Craftsmanship"
            value={0}
            required={sufficiency.craftsmanshipDeficit > 0 ? sufficiency.craftsmanshipDeficit : undefined}
          />
        )}
        {sufficiency.controlDeficit !== 0 && (
          <StatBar
            label="Control"
            value={0}
            required={sufficiency.controlDeficit > 0 ? sufficiency.controlDeficit : undefined}
          />
        )}
      </div>

      {!sufficiency.meetsMinimumRequirements && (
        <p className="text-xs text-red-400">
          Your stats are below the minimum requirements. Upgrade gear or level up before attempting this craft.
        </p>
      )}

      {sufficiency.canComplete && (
        <div className="flex flex-wrap gap-2">
          <Link
            href={`/simulator?recipeId=${recipeId}&solve=true`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md bg-bg-elevated text-text-secondary hover:text-gold transition-colors"
          >
            <Sparkles size={12} />
            Auto-Solve
          </Link>
          <Link
            href={`/simulator?recipeId=${recipeId}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md bg-bg-elevated text-text-secondary hover:text-gold transition-colors"
          >
            <FlaskConical size={12} />
            Manual Craft
          </Link>
        </div>
      )}
    </div>
  );
}
