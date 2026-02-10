"use client";

import { useState } from "react";
import { Pencil, Check, X } from "lucide-react";
import { ItemIcon } from "@/components/ui/ItemIcon";
import {
  DOH_JOBS,
  DOL_JOBS,
  JOB_INFO,
  getJobIconUrl,
  type GearJob,
} from "@/lib/gear-constants";
import { updateJobLevels } from "@/lib/api-client";
import { LoadingSpinner } from "@/components/ui/LoadingSpinner";
import type { JobLevelDto } from "@/types/profile";

interface JobLevelsGridProps {
  jobLevels: Record<string, JobLevelDto>;
  onUpdate: () => void;
}

export function JobLevelsGrid({ jobLevels, onUpdate }: JobLevelsGridProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [editValues, setEditValues] = useState<Record<string, number>>({});
  const [isSaving, setIsSaving] = useState(false);

  function startEditing() {
    const values: Record<string, number> = {};
    for (const job of [...DOH_JOBS, ...DOL_JOBS]) {
      values[job] = jobLevels[job]?.level ?? 0;
    }
    setEditValues(values);
    setIsEditing(true);
  }

  async function saveEdits() {
    setIsSaving(true);
    try {
      const changed: Record<string, number> = {};
      for (const [job, level] of Object.entries(editValues)) {
        const current = jobLevels[job]?.level ?? 0;
        if (level !== current) {
          changed[job] = level;
        }
      }
      if (Object.keys(changed).length > 0) {
        await updateJobLevels(changed);
        onUpdate();
      }
      setIsEditing(false);
    } catch {
      // Keep editing mode open on error
    } finally {
      setIsSaving(false);
    }
  }

  function cancelEditing() {
    setIsEditing(false);
    setEditValues({});
  }

  return (
    <div className="p-4 bg-bg-surface rounded-lg border border-border space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-text-primary">Job Levels</h3>
        {isEditing ? (
          <div className="flex items-center gap-2">
            <button
              onClick={cancelEditing}
              disabled={isSaving}
              className="flex items-center gap-1 px-2 py-1 text-xs text-text-muted hover:text-text-secondary transition-colors"
            >
              <X size={14} />
              Cancel
            </button>
            <button
              onClick={saveEdits}
              disabled={isSaving}
              className="flex items-center gap-1 px-3 py-1 text-xs font-medium text-gold hover:text-gold-light transition-colors disabled:opacity-50"
            >
              {isSaving ? <LoadingSpinner size={14} /> : <Check size={14} />}
              Save
            </button>
          </div>
        ) : (
          <button
            onClick={startEditing}
            className="flex items-center gap-1 px-2 py-1 text-xs text-text-muted hover:text-gold transition-colors"
          >
            <Pencil size={12} />
            Edit
          </button>
        )}
      </div>

      <JobSection
        label="Disciples of the Hand"
        jobs={DOH_JOBS}
        jobLevels={jobLevels}
        isEditing={isEditing}
        editValues={editValues}
        onEditChange={(job, val) => setEditValues((prev) => ({ ...prev, [job]: val }))}
      />

      <JobSection
        label="Disciples of the Land"
        jobs={DOL_JOBS}
        jobLevels={jobLevels}
        isEditing={isEditing}
        editValues={editValues}
        onEditChange={(job, val) => setEditValues((prev) => ({ ...prev, [job]: val }))}
      />
    </div>
  );
}

function JobSection({
  label,
  jobs,
  jobLevels,
  isEditing,
  editValues,
  onEditChange,
}: {
  label: string;
  jobs: GearJob[];
  jobLevels: Record<string, JobLevelDto>;
  isEditing: boolean;
  editValues: Record<string, number>;
  onEditChange: (job: string, value: number) => void;
}) {
  return (
    <div>
      <p className="text-xs uppercase tracking-wider text-text-muted mb-2">{label}</p>
      <div className="grid grid-cols-4 sm:grid-cols-8 gap-2">
        {jobs.map((job) => {
          const info = JOB_INFO[job];
          const level = jobLevels[job]?.level ?? 0;
          const isDimmed = !isEditing && level === 0;

          return (
            <div
              key={job}
              className={`flex flex-col items-center gap-1 p-1.5 rounded-lg ${isDimmed ? "opacity-40" : ""}`}
            >
              <ItemIcon
                iconUrl={getJobIconUrl(info.classJobId)}
                alt={info.fullName}
                size="lg"
              />
              <span className={`text-xs font-medium ${info.colorClass}`}>{job}</span>
              {isEditing ? (
                <input
                  type="number"
                  min={0}
                  max={100}
                  value={editValues[job] ?? 0}
                  onChange={(e) => {
                    const v = parseInt(e.target.value, 10);
                    onEditChange(job, isNaN(v) ? 0 : Math.max(0, Math.min(100, v)));
                  }}
                  className="w-12 px-1 py-0.5 text-center text-xs bg-bg-secondary border border-border rounded focus:outline-none focus:border-border-focus text-text-primary"
                />
              ) : (
                <span className="text-xs text-text-muted">{level}</span>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
