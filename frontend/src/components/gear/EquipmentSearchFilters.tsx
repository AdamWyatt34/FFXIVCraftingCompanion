"use client";

import { X } from "lucide-react";
import { JOB_INFO, DOH_JOBS, DOL_JOBS, type GearJob } from "@/lib/gear-constants";

const SLOT_OPTIONS = [
  "Head", "Body", "Hands", "Legs", "Feet",
  "MainHand", "OffHand",
  "Earring", "Necklace", "Bracelet", "Ring",
] as const;

const SLOT_DISPLAY: Record<string, string> = {
  MainHand: "Main Hand",
  OffHand: "Off Hand",
};

interface EquipmentSearchFiltersProps {
  jobFilter: string | null;
  onJobFilterChange: (job: string | null) => void;
  slotFilter: string | null;
  onSlotFilterChange: (slot: string | null) => void;
}

export function EquipmentSearchFilters({
  jobFilter,
  onJobFilterChange,
  slotFilter,
  onSlotFilterChange,
}: EquipmentSearchFiltersProps) {
  return (
    <div className="space-y-3">
      {/* Active filters */}
      {(jobFilter || slotFilter) && (
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-xs text-text-muted">Filters:</span>
          {jobFilter && (
            <button
              onClick={() => onJobFilterChange(null)}
              className="inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium bg-gold/15 text-gold hover:bg-gold/25 transition-colors"
            >
              {JOB_INFO[jobFilter as GearJob]?.fullName ?? jobFilter}
              <X size={12} />
            </button>
          )}
          {slotFilter && (
            <button
              onClick={() => onSlotFilterChange(null)}
              className="inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium bg-blue-500/15 text-blue-400 hover:bg-blue-500/25 transition-colors"
            >
              {SLOT_DISPLAY[slotFilter] ?? slotFilter}
              <X size={12} />
            </button>
          )}
        </div>
      )}

      {/* Slot filter */}
      <div role="group" aria-label="Filter by equipment slot">
        <p className="text-xs text-text-muted mb-1.5">Slot</p>
        <div className="flex flex-wrap gap-1.5">
          {SLOT_OPTIONS.map((slot) => (
            <button
              key={slot}
              onClick={() => onSlotFilterChange(slotFilter === slot ? null : slot)}
              className={`px-2.5 py-1 rounded-md text-xs font-medium transition-colors min-h-[32px] ${
                slotFilter === slot
                  ? "bg-blue-500/20 text-blue-400 ring-1 ring-blue-500/30"
                  : "bg-bg-elevated text-text-secondary hover:text-text-primary hover:bg-bg-elevated/80"
              }`}
              aria-pressed={slotFilter === slot}
            >
              {SLOT_DISPLAY[slot] ?? slot}
            </button>
          ))}
        </div>
      </div>

      {/* Job filter */}
      <div role="group" aria-label="Filter by job">
        <p className="text-xs text-text-muted mb-1.5">Job</p>
        <div className="flex flex-wrap gap-1.5">
          {[...DOH_JOBS, ...DOL_JOBS].map((job) => {
            const info = JOB_INFO[job];
            return (
              <button
                key={job}
                onClick={() => onJobFilterChange(jobFilter === job ? null : job)}
                className={`px-2.5 py-1 rounded-md text-xs font-medium transition-colors min-h-[32px] ${
                  jobFilter === job
                    ? "bg-gold/20 text-gold ring-1 ring-gold/30"
                    : "bg-bg-elevated text-text-secondary hover:text-text-primary hover:bg-bg-elevated/80"
                }`}
                aria-pressed={jobFilter === job}
              >
                {info.abbreviation}
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
}
