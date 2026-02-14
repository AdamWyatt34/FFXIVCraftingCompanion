"use client";

import { useState } from "react";
import { User, Shield, ChefHat, FlaskConical, Trash2, Sparkles } from "lucide-react";
import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import { useProfile } from "@/hooks/useProfile";
import {
  AuthGuard,
  ProfileHeader,
  ProfileStats,
  JobLevelsGrid,
  ProfileSetupWizard,
  SavedListsTabs,
  ProfileLoadingSkeleton,
  ProfileRotationsList,
} from "@/components/profile";
import { ErrorState } from "@/components/ui";
import { deleteProfile, updateProfile } from "@/lib/api-client";
import { JOB_INFO, type GearJob } from "@/lib/gear-constants";

function ProfileDashboardContent() {
  const { logout } = useAuth();
  const { profile, isLoading, error, retry, invalidate } = useProfile();
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [switchJobOpen, setSwitchJobOpen] = useState(false);

  if (isLoading) return <ProfileLoadingSkeleton />;
  if (error) return <ErrorState message={error} onRetry={retry} />;

  if (!profile) {
    return <ProfileSetupWizard onComplete={invalidate} />;
  }

  async function handleSwitchJob(job: string) {
    const info = JOB_INFO[job as GearJob];
    if (!info || !profile) return;

    const jobLevel = profile.jobLevels[job]?.level ?? 1;
    try {
      await updateProfile({ activeJob: job, activeJobLevel: jobLevel });
      invalidate();
    } catch {
      // Ignore
    }
    setSwitchJobOpen(false);
  }

  async function handleDeleteProfile() {
    setIsDeleting(true);
    try {
      await deleteProfile();
      invalidate();
    } catch {
      // Ignore
    } finally {
      setIsDeleting(false);
      setShowDeleteConfirm(false);
    }
  }

  const availableJobs = Object.entries(profile.jobLevels)
    .filter(([, jl]) => jl.level > 0 && jl.job !== profile.activeJob)
    .sort(([, a], [, b]) => b.level - a.level);

  return (
    <div className="flex-1 overflow-y-auto p-4">
      <div className="space-y-6 max-w-4xl mx-auto">
        <ProfileHeader profile={profile} onRefresh={invalidate} />

        {/* Switch job */}
        {availableJobs.length > 0 && (
          <div>
            <button
              onClick={() => setSwitchJobOpen(!switchJobOpen)}
              className="text-sm text-text-muted hover:text-gold transition-colors"
            >
              Switch active job ({profile.activeJob})
            </button>
            {switchJobOpen && (
              <div className="mt-2 flex flex-wrap gap-2 animate-[fade-in_0.2s_ease-out]">
                {availableJobs.map(([key, jl]) => (
                  <button
                    key={key}
                    onClick={() => handleSwitchJob(key)}
                    className="px-3 py-1.5 rounded-full text-xs font-medium bg-bg-surface border border-border text-text-secondary hover:border-border-focus transition-colors"
                  >
                    {key} Lv.{jl.level}
                  </button>
                ))}
              </div>
            )}
          </div>
        )}

        <ProfileStats stats={profile.stats} />
        <JobLevelsGrid jobLevels={profile.jobLevels} onUpdate={invalidate} />

        {/* Quick actions */}
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
          <Link
            href={`/gear?job=${profile.activeJob}&level=${profile.activeJobLevel}`}
            className="flex items-center gap-2 p-3 rounded-lg bg-bg-surface border border-border hover:border-border-focus transition-colors"
          >
            <Shield className="text-gold shrink-0" size={18} />
            <span className="text-sm text-text-primary">Gear Recommendations</span>
          </Link>
          <Link
            href="/recipes"
            className="flex items-center gap-2 p-3 rounded-lg bg-bg-surface border border-border hover:border-border-focus transition-colors"
          >
            <ChefHat className="text-gold shrink-0" size={18} />
            <span className="text-sm text-text-primary">Browse Recipes</span>
          </Link>
          <Link
            href={`/simulator?craftsmanship=${profile.stats.craftsmanship}&control=${profile.stats.control}&cp=${profile.stats.cp}&level=${profile.activeJobLevel}`}
            className="flex items-center gap-2 p-3 rounded-lg bg-bg-surface border border-border hover:border-border-focus transition-colors"
          >
            <FlaskConical className="text-gold shrink-0" size={18} />
            <span className="text-sm text-text-primary">Crafting Simulator</span>
          </Link>
        </div>

        {/* My Rotations */}
        <div>
          <div className="flex items-center gap-2 mb-4">
            <Sparkles size={18} className="text-gold" />
            <h2 className="text-lg font-bold text-text-primary">My Rotations</h2>
          </div>
          <ProfileRotationsList />
        </div>

        {/* Saved content */}
        <div>
          <h2 className="text-lg font-bold text-text-primary mb-4">Saved Content</h2>
          <SavedListsTabs />
        </div>

        {/* Account actions */}
        <div className="border-t border-border pt-4 space-y-3">
          <button
            onClick={() => logout()}
            className="w-full h-10 text-sm text-text-muted hover:text-text-secondary border border-border rounded-lg transition-colors"
          >
            Sign Out
          </button>

          {!showDeleteConfirm ? (
            <button
              onClick={() => setShowDeleteConfirm(true)}
              className="w-full h-10 text-sm text-red-400/60 hover:text-red-400 transition-colors"
            >
              Delete Profile
            </button>
          ) : (
            <div className="flex items-center gap-3 p-3 bg-red-500/10 border border-red-500/30 rounded-lg">
              <Trash2 className="text-red-400 shrink-0" size={16} />
              <span className="text-sm text-red-400 flex-1">Are you sure?</span>
              <button
                onClick={() => setShowDeleteConfirm(false)}
                className="px-3 py-1 text-xs text-text-muted hover:text-text-secondary transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={handleDeleteProfile}
                disabled={isDeleting}
                className="px-3 py-1 text-xs font-medium text-red-400 hover:text-red-300 transition-colors disabled:opacity-50"
              >
                {isDeleting ? "Deleting..." : "Delete"}
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export default function ProfilePage() {
  return (
    <div className="flex flex-col h-full">
      <div className="p-4 border-b border-border shrink-0">
        <div className="flex items-center gap-2 mb-1">
          <User className="text-gold" size={24} />
          <h1 className="text-xl font-bold">Profile</h1>
        </div>
        <p className="text-text-secondary text-sm">
          Your character, stats, and saved content
        </p>
      </div>
      <AuthGuard>
        <ProfileDashboardContent />
      </AuthGuard>
    </div>
  );
}
