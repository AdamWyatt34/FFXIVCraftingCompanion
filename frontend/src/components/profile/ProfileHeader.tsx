"use client";

import { useState } from "react";
import Image from "next/image";
import { User, RefreshCw } from "lucide-react";
import { ItemIcon } from "@/components/ui/ItemIcon";
import { LoadingSpinner } from "@/components/ui/LoadingSpinner";
import { JOB_INFO, getJobIconUrl, type GearJob } from "@/lib/gear-constants";
import { refreshLodestone, ApiClientError, trackAnalyticsEvent } from "@/lib/api-client";
import type { CharacterProfileDto } from "@/types/profile";

interface ProfileHeaderProps {
  profile: CharacterProfileDto;
  onRefresh: () => void;
}

export function ProfileHeader({ profile, onRefresh }: ProfileHeaderProps) {
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [refreshError, setRefreshError] = useState<string | null>(null);

  const jobInfo = JOB_INFO[profile.activeJob as GearJob];

  async function handleRefreshLodestone() {
    setIsRefreshing(true);
    setRefreshError(null);
    try {
      await refreshLodestone();
      trackAnalyticsEvent("profile_import", { source: "lodestone_refresh" });
      onRefresh();
    } catch (err) {
      if (err instanceof ApiClientError) {
        setRefreshError(
          err.status === 429
            ? "Please wait 15 minutes between Lodestone refreshes."
            : err.body.error || "Failed to refresh from Lodestone.",
        );
      } else {
        setRefreshError("An unexpected error occurred.");
      }
    } finally {
      setIsRefreshing(false);
    }
  }

  const lastSync = profile.lastLodestoneSync
    ? new Date(profile.lastLodestoneSync).toLocaleDateString(undefined, {
        month: "short",
        day: "numeric",
        hour: "2-digit",
        minute: "2-digit",
      })
    : null;

  return (
    <div className="p-4 bg-bg-surface rounded-lg border border-border space-y-3">
      <div className="flex items-center gap-4">
        {/* Avatar */}
        <div className="w-16 h-16 shrink-0 rounded-full border-2 border-gold/40 overflow-hidden bg-bg-elevated flex items-center justify-center">
          {profile.avatarUrl ? (
            <Image
              src={profile.avatarUrl}
              alt={profile.characterName}
              width={64}
              height={64}
              className="w-full h-full object-cover"
            />
          ) : (
            <User className="text-text-muted" size={32} />
          )}
        </div>

        {/* Info */}
        <div className="flex-1 min-w-0">
          <h2 className="text-lg font-bold text-text-primary truncate">
            {profile.characterName}
          </h2>
          <div className="flex items-center gap-2 text-sm text-text-secondary">
            {profile.serverName && <span>{profile.serverName}</span>}
            {profile.dataCenter && (
              <span className="text-text-muted">({profile.dataCenter})</span>
            )}
          </div>
          <div className="flex items-center gap-2 mt-1">
            {jobInfo && (
              <div className="flex items-center gap-1.5">
                <ItemIcon
                  iconUrl={getJobIconUrl(jobInfo.classJobId)}
                  alt={jobInfo.fullName}
                  size="sm"
                />
                <span className={`text-sm font-medium ${jobInfo.colorClass}`}>
                  {jobInfo.fullName}
                </span>
                <span className="text-sm text-text-muted">Lv. {profile.activeJobLevel}</span>
              </div>
            )}
            <span className="text-xs px-2 py-0.5 rounded-full bg-bg-elevated text-text-muted">
              {profile.source}
            </span>
          </div>
        </div>
      </div>

      {/* Lodestone refresh */}
      {(profile.source === "Lodestone" || profile.source === "Mixed") && (
        <div className="flex items-center justify-between pt-2 border-t border-border">
          <div className="text-xs text-text-muted">
            {lastSync ? `Last synced: ${lastSync}` : "Linked to Lodestone"}
          </div>
          <button
            onClick={handleRefreshLodestone}
            disabled={isRefreshing}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-gold hover:text-gold-light transition-colors disabled:opacity-50"
          >
            {isRefreshing ? (
              <LoadingSpinner size={14} />
            ) : (
              <RefreshCw size={14} />
            )}
            Refresh
          </button>
        </div>
      )}

      {refreshError && (
        <div className="p-2 rounded bg-red-500/10 border border-red-500/30 text-red-400 text-xs">
          {refreshError}
        </div>
      )}
    </div>
  );
}
