"use client";

import Link from "next/link";
import { Sparkles, Diamond, Hammer } from "lucide-react";
import { ItemIcon } from "@/components/ui/ItemIcon";
import { SourceBadge } from "@/components/ui/SourceBadge";
import type { EquipmentDto } from "@/types/gear";
import { RARITY_COLOR_CLASS, JOB_INFO, DOH_JOBS, DOL_JOBS, type GearJob } from "@/lib/gear-constants";

interface EquipmentSearchCardProps {
  equipment: EquipmentDto;
  onClick: () => void;
  isExpanded: boolean;
}

const ALL_JOBS = [...DOH_JOBS, ...DOL_JOBS];

function parseJobChips(classJobCategory: string): GearJob[] {
  if (!classJobCategory) return [];
  return ALL_JOBS.filter((job) => classJobCategory.includes(job));
}

export function EquipmentSearchCard({
  equipment,
  onClick,
  isExpanded,
}: EquipmentSearchCardProps) {
  const rarityClass =
    RARITY_COLOR_CLASS[equipment.rarityName] ?? "text-text-primary";
  const isDoH = equipment.stats.craftsmanship > 0;
  const jobs = parseJobChips(equipment.classJobCategory);

  return (
    <div
      className={`rounded-lg border bg-bg-surface p-3 space-y-2 cursor-pointer transition-all ${
        isExpanded
          ? "border-gold ring-1 ring-gold/30"
          : "border-border hover:border-gold/50 hover:bg-bg-elevated/50"
      }`}
      onClick={onClick}
      role="button"
      tabIndex={0}
      aria-expanded={isExpanded}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          onClick();
        }
      }}
    >
      {/* Main item row */}
      <div className="flex items-center gap-3">
        <ItemIcon iconUrl={equipment.iconUrl} alt={equipment.name} size="lg" />
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <p className={`font-medium truncate ${rarityClass}`}>
              {equipment.name}
            </p>
            {equipment.canBeHQ && (
              <span title={equipment.hqNote ?? "HQ version available"}>
                <Sparkles size={14} className="text-gold shrink-0" />
              </span>
            )}
          </div>
          <div className="flex items-center gap-2 text-xs text-text-secondary">
            <span className="bg-bg-elevated px-1.5 py-0.5 rounded text-text-primary">
              iLv {equipment.itemLevel}
            </span>
            <span className="text-text-muted">Lv. {equipment.equipLevel}</span>
            {equipment.materiaSlots > 0 && (
              <span
                className="flex items-center gap-0.5"
                title={`${equipment.materiaSlots} materia slot${equipment.materiaSlots > 1 ? "s" : ""}`}
              >
                {Array.from({ length: equipment.materiaSlots }, (_, i) => (
                  <Diamond
                    key={i}
                    size={8}
                    className="text-blue-400 fill-blue-400"
                  />
                ))}
              </span>
            )}
          </div>
        </div>
      </div>

      {/* Stats row */}
      <div className="flex flex-wrap gap-x-3 gap-y-1 text-xs">
        {isDoH ? (
          <>
            {equipment.stats.craftsmanship > 0 && (
              <StatChip label="Craft" value={equipment.stats.craftsmanship} />
            )}
            {equipment.stats.control > 0 && (
              <StatChip label="Ctrl" value={equipment.stats.control} />
            )}
            {equipment.stats.cp > 0 && (
              <StatChip label="CP" value={equipment.stats.cp} />
            )}
          </>
        ) : (
          <>
            {equipment.stats.gathering > 0 && (
              <StatChip label="Gath" value={equipment.stats.gathering} />
            )}
            {equipment.stats.perception > 0 && (
              <StatChip label="Perc" value={equipment.stats.perception} />
            )}
            {equipment.stats.gp > 0 && (
              <StatChip label="GP" value={equipment.stats.gp} />
            )}
          </>
        )}
      </div>

      {/* Job chips */}
      {jobs.length > 0 && (
        <div className="flex flex-wrap gap-1">
          {jobs.map((job) => (
            <span
              key={job}
              className={`px-1.5 py-0.5 rounded text-xs ${JOB_INFO[job].colorClass} bg-bg-elevated`}
            >
              {job}
            </span>
          ))}
        </div>
      )}

      {/* Source badge */}
      {equipment.source && (
        <div className="flex items-center gap-2">
          {equipment.source.hasRecipe && equipment.source.recipeId ? (
            <Link
              href={`/recipes/${equipment.source.recipeId}?from=gear`}
              onClick={(e) => e.stopPropagation()}
            >
              <span className="inline-flex items-center gap-1 group">
                <SourceBadge
                  sourceType={equipment.source.type}
                  className="group-hover:opacity-80 transition-opacity"
                />
                <Hammer
                  size={12}
                  className="text-gold opacity-0 group-hover:opacity-100 transition-opacity"
                />
              </span>
            </Link>
          ) : (
            <SourceBadge sourceType={equipment.source.type} />
          )}
          <span className="text-xs text-text-muted truncate flex-1">
            {equipment.source.description}
          </span>
        </div>
      )}
    </div>
  );
}

function StatChip({ label, value }: { label: string; value: number }) {
  return (
    <span className="text-text-secondary">
      <span className="text-text-muted">{label}</span>{" "}
      <span className="text-text-primary font-medium">{value}</span>
    </span>
  );
}
