"use client";

import { useState } from "react";
import { Bookmark } from "lucide-react";
import { ProtectedAction } from "@/components/ui/ProtectedAction";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { useToast } from "@/components/ui/Toast";
import { saveGearSet } from "@/lib/api-client";
import { gearSetToProfileSlots } from "@/lib/slot-mapping";
import { JOB_INFO, PROFILE_DISPLAY_NAMES, type GearJob } from "@/lib/gear-constants";
import type { GearSetDto } from "@/types/gear";

interface SaveGearSetButtonProps {
  gearSet: GearSetDto;
}

export function SaveGearSetButton({ gearSet }: SaveGearSetButtonProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const { toast } = useToast();

  const jobInfo = JOB_INFO[gearSet.job as GearJob];
  const jobName = jobInfo?.fullName ?? gearSet.job;
  const profileDisplayName =
    PROFILE_DISPLAY_NAMES[gearSet.profile.name] ?? gearSet.profile.displayName;

  const defaultName = `${jobName} Lv.${gearSet.level} ${profileDisplayName} Set`;
  const [name, setName] = useState(defaultName);

  const handleOpen = () => {
    setName(defaultName);
    setIsOpen(true);
  };

  const handleSave = async () => {
    if (!name.trim()) return;
    setIsLoading(true);
    try {
      const slots = gearSetToProfileSlots(gearSet.slots);
      await saveGearSet({ name: name.trim(), job: gearSet.job, slots });
      toast("Gear set saved!", "success");
      setIsOpen(false);
    } catch {
      toast("Failed to save gear set", "error");
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <>
      <ProtectedAction message="Sign in to save gear sets">
        <button
          onClick={handleOpen}
          className="flex items-center gap-2 px-3 py-2 rounded-lg bg-bg-elevated border border-border text-text-secondary hover:text-gold hover:border-gold/50 transition-colors text-sm"
          aria-label="Save gear set"
        >
          <Bookmark size={16} />
          Save
        </button>
      </ProtectedAction>

      <ConfirmDialog
        open={isOpen}
        title="Save gear set"
        confirmLabel="Save"
        isLoading={isLoading}
        onConfirm={handleSave}
        onCancel={() => setIsOpen(false)}
      >
        <div className="space-y-2">
          <label
            htmlFor="gear-set-name"
            className="block text-sm font-medium text-text-secondary"
          >
            Name
          </label>
          <input
            id="gear-set-name"
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            className="w-full px-3 py-2 rounded-lg bg-bg-primary border border-border text-text-primary text-sm focus:outline-none focus:border-gold/50 focus:ring-1 focus:ring-gold/30"
            autoFocus
            onKeyDown={(e) => {
              if (e.key === "Enter" && name.trim()) {
                e.preventDefault();
                handleSave();
              }
            }}
          />
        </div>
      </ConfirmDialog>
    </>
  );
}
