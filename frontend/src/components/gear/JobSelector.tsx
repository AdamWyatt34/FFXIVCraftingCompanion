"use client";

import { ItemIcon } from "@/components/ui/ItemIcon";
import {
  DOH_JOBS,
  DOL_JOBS,
  TANK_JOBS,
  HEALER_JOBS,
  MELEE_DPS_JOBS,
  RANGED_DPS_JOBS,
  MAGIC_DPS_JOBS,
  JOB_INFO,
  getJobIconUrl,
  type GearJob,
} from "@/lib/gear-constants";

interface JobSelectorProps {
  selectedJob: string | null;
  onJobSelect: (job: string) => void;
}

export function JobSelector({ selectedJob, onJobSelect }: JobSelectorProps) {
  return (
    <div className="space-y-4">
      <JobRow label="Disciples of the Hand" jobs={DOH_JOBS} selectedJob={selectedJob} onSelect={onJobSelect} />
      <JobRow label="Disciples of the Land" jobs={DOL_JOBS} selectedJob={selectedJob} onSelect={onJobSelect} />
      <JobRow label="Tank" jobs={TANK_JOBS} selectedJob={selectedJob} onSelect={onJobSelect} />
      <JobRow label="Healer" jobs={HEALER_JOBS} selectedJob={selectedJob} onSelect={onJobSelect} />
      <JobRow label="Melee DPS" jobs={MELEE_DPS_JOBS} selectedJob={selectedJob} onSelect={onJobSelect} />
      <JobRow label="Ranged DPS" jobs={RANGED_DPS_JOBS} selectedJob={selectedJob} onSelect={onJobSelect} />
      <JobRow label="Magic DPS" jobs={MAGIC_DPS_JOBS} selectedJob={selectedJob} onSelect={onJobSelect} />
    </div>
  );
}

function JobRow({
  label,
  jobs,
  selectedJob,
  onSelect,
}: {
  label: string;
  jobs: GearJob[];
  selectedJob: string | null;
  onSelect: (job: string) => void;
}) {
  return (
    <div>
      <p className="text-xs uppercase tracking-wider text-text-muted mb-2">{label}</p>
      <div className="grid grid-cols-4 sm:flex sm:flex-wrap gap-2">
        {jobs.map((job) => {
          const info = JOB_INFO[job];
          const isSelected = selectedJob === job;
          return (
            <button
              key={job}
              onClick={() => onSelect(job)}
              className={`flex flex-col items-center gap-1 p-2 rounded-lg border transition-all ${
                isSelected
                  ? "border-gold bg-bg-elevated shadow-[0_0_8px_rgba(230,180,34,0.3)]"
                  : "border-border bg-bg-surface opacity-70 hover:opacity-100 hover:border-text-muted"
              }`}
              title={info.fullName}
            >
              <ItemIcon
                iconUrl={getJobIconUrl(info.classJobId)}
                alt={info.fullName}
                size="xl"
              />
              <span className={`text-xs font-medium ${isSelected ? "text-gold" : info.colorClass}`}>
                {job}
              </span>
            </button>
          );
        })}
      </div>
    </div>
  );
}
