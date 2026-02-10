"use client";

import { Suspense, useState } from "react";
import { useSearchParams } from "next/navigation";
import { Shield, User } from "lucide-react";
import { useGearRecommendations } from "@/hooks/useGearRecommendations";
import { useGearComparison } from "@/hooks/useGearComparison";
import { useEquipmentSearch } from "@/hooks/useEquipmentSearch";
import { useProfile } from "@/hooks/useProfile";
import { useAuth } from "@/contexts/AuthContext";
import {
  JobSelector,
  LevelInput,
  ProfileSelector,
  GearGrid,
  StatsSummary,
  GearLoadingSkeleton,
  GearModeToggle,
  EquipmentSearchFilters,
  EquipmentSearchCard,
  GearEmptyState,
  GearShareButton,
  UpgradeSummaryBanner,
  SaveGearSetButton,
  ApplyGearDialog,
} from "@/components/gear";
import { GearSlotExpandedContent, BottomSheet } from "@/components/gear";
import { SearchBar } from "@/components/ui/SearchBar";
import { ErrorState, LoadingSkeleton } from "@/components/ui/LoadingStates";
import { JOB_INFO, type GearJob } from "@/lib/gear-constants";
import { gearSetToProfileSlots } from "@/lib/slot-mapping";
import { useMediaQuery } from "@/hooks/useMediaQuery";
import type { EquipmentDto, GearSlotDto } from "@/types/gear";

function GearPageContent() {
  const searchParams = useSearchParams();

  // Auth & profile
  const { isAuthenticated } = useAuth();
  const { profile: userProfile, invalidate: invalidateProfile } = useProfile();

  // Read URL params for deep-linking / sharing
  const urlJob = searchParams.get("job");
  const urlLevel = searchParams.get("level");
  const urlProfile = searchParams.get("profile");
  const urlMode = searchParams.get("mode");
  const urlSearch = searchParams.get("search");

  // Auto-fill from profile when no URL job param
  const autoFilledFromProfile = !urlJob && !!userProfile;
  const initialJob = urlJob ?? (userProfile?.activeJob || null);
  const initialLevel = urlLevel
    ? Number(urlLevel)
    : autoFilledFromProfile
      ? userProfile?.activeJobLevel
      : undefined;

  const initialMode = urlMode === "search" ? "search" : "recommend";
  const [mode, setMode] = useState<"recommend" | "search">(initialMode);

  const {
    job,
    setJob,
    level,
    setLevel,
    profile,
    setProfile,
    profiles,
    isLoadingProfiles,
    data,
    isLoading,
    error,
    retry,
  } = useGearRecommendations({
    initialJob: initialJob,
    initialLevel: initialLevel,
    initialProfile: urlProfile ?? undefined,
    jobLevels: userProfile?.jobLevels,
  });

  // Gear comparison (only when authenticated with gear data)
  const hasProfileGear =
    isAuthenticated && !!userProfile?.currentGear?.length;
  const {
    comparison: comparisonData,
    invalidate: invalidateComparison,
  } = useGearComparison(job, level, profile, hasProfileGear);

  // Apply gear dialog state
  const [applyDialog, setApplyDialog] = useState<{
    open: boolean;
    slots: Record<string, number>;
    mode: "all" | "single";
    slotName?: string;
  }>({ open: false, slots: {}, mode: "all" });

  const equipSearch = useEquipmentSearch(urlSearch ?? "");
  const isDesktop = useMediaQuery("(min-width: 640px)");

  // Search result expansion
  const [expandedSearchIdx, setExpandedSearchIdx] = useState<number | null>(null);

  const jobInfo = job ? JOB_INFO[job as GearJob] : null;
  const isDoH = jobInfo?.category === "DoH";

  const handleApplyAll = () => {
    if (!data) return;
    const slots = gearSetToProfileSlots(data.slots);
    setApplyDialog({ open: true, slots, mode: "all" });
  };

  const handleApplySingle = (profileSlotKey: string, equipmentId: number) => {
    setApplyDialog({
      open: true,
      slots: { [profileSlotKey]: equipmentId },
      mode: "single",
      slotName: profileSlotKey,
    });
  };

  const handleApplySuccess = () => {
    invalidateProfile();
    invalidateComparison();
  };

  // Create a wrapper slot for the expanded search result
  const expandedSearchSlot: GearSlotDto | null =
    expandedSearchIdx !== null && equipSearch.results[expandedSearchIdx]
      ? {
          slotName: equipSearch.results[expandedSearchIdx].name,
          slotKey: "Search",
          recommendation: equipSearch.results[expandedSearchIdx],
          alternatives: [],
          isEmpty: false,
        }
      : null;

  // Detect if the expanded equipment is DoH based on stats
  const expandedIsDoH =
    expandedSearchSlot?.recommendation
      ? expandedSearchSlot.recommendation.stats.craftsmanship > 0
      : true;

  return (
    <div className="flex flex-col h-full">
      {/* Page header */}
      <div className="p-4 border-b border-border">
        <div className="flex items-center gap-2 mb-1">
          <Shield className="text-gold" size={24} />
          <h1 className="text-xl font-bold">Gear Recommendations</h1>
        </div>
        <p className="text-text-secondary text-sm">
          Find the best DoH/DoL equipment for your level
        </p>
      </div>

      {/* Main content */}
      <div className="flex-1 overflow-y-auto p-4">
        <div className="space-y-6 max-w-4xl mx-auto">
          {/* Mode toggle */}
          <GearModeToggle mode={mode} onModeChange={setMode} />

          {mode === "recommend" ? (
            <>
              {/* Profile auto-fill banner */}
              {autoFilledFromProfile && job && userProfile && (
                <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-bg-elevated border border-border text-sm text-text-secondary">
                  <User size={14} className="text-gold shrink-0" />
                  <span>
                    Auto-filled from your profile:{" "}
                    <span className="text-text-primary font-medium">
                      {userProfile.activeJobDisplayName} Lv.{userProfile.activeJobLevel}
                    </span>
                  </span>
                </div>
              )}

              {/* Step 1: Job selector */}
              {!job && (
                <p className="text-text-secondary text-center">
                  Select your crafting or gathering job to see gear
                  recommendations
                </p>
              )}
              <JobSelector selectedJob={job} onJobSelect={setJob} />

              {/* Step 2: Level + Profile (after job selected) */}
              {job && (
                <div className="space-y-4 animate-slide-up">
                  <div className="flex flex-col sm:flex-row sm:items-end gap-4">
                    <LevelInput value={level} onChange={setLevel} />
                    <div className="flex-1">
                      <ProfileSelector
                        profiles={profiles}
                        selectedProfile={profile}
                        onProfileSelect={setProfile}
                        isLoading={isLoadingProfiles}
                      />
                    </div>
                  </div>
                </div>
              )}

              {/* Step 3: Recommendations */}
              {isLoading && <GearLoadingSkeleton />}
              {error && <ErrorState message={error} onRetry={retry} />}
              {data && (
                <div className="space-y-4 animate-slide-up">
                  <div className="flex items-start justify-between gap-4">
                    <div className="flex-1">
                      <StatsSummary
                        stats={data.totalStats}
                        isDoH={isDoH}
                        profileName={data.profile.displayName}
                      />
                    </div>
                    <div className="flex items-center gap-2">
                      <SaveGearSetButton gearSet={data} />
                      <GearShareButton
                        job={data.job}
                        level={data.level}
                        profile={data.profile.name}
                        gearSet={data}
                      />
                    </div>
                  </div>
                  {comparisonData?.upgradeSummary && (
                    <UpgradeSummaryBanner
                      summary={comparisonData.upgradeSummary}
                      isDoH={isDoH}
                      onApplyAll={handleApplyAll}
                      isAuthenticated={isAuthenticated}
                    />
                  )}
                  {level < 10 && (
                    <GearEmptyState type="low-level" />
                  )}
                  <GearGrid
                    slots={data.slots}
                    isDoH={isDoH}
                    slotComparisons={comparisonData?.slotComparisons}
                    onApplySingle={handleApplySingle}
                  />
                </div>
              )}
            </>
          ) : (
            /* Search mode */
            <div className="space-y-4">
              <SearchBar
                value={equipSearch.query}
                onChange={equipSearch.setQuery}
                isLoading={equipSearch.isLoading}
                placeholder="Search for gear..."
                autoFocus
              />
              <EquipmentSearchFilters
                jobFilter={equipSearch.jobFilter}
                onJobFilterChange={equipSearch.setJobFilter}
                slotFilter={equipSearch.slotFilter}
                onSlotFilterChange={equipSearch.setSlotFilter}
              />

              {/* Search results */}
              {equipSearch.error && (
                <ErrorState
                  message={equipSearch.error}
                  onRetry={equipSearch.retry}
                />
              )}
              {!equipSearch.error &&
                equipSearch.query.length >= 2 &&
                !equipSearch.isLoading &&
                equipSearch.results.length === 0 && (
                  <GearEmptyState type="search-empty" />
                )}
              {equipSearch.isLoading && equipSearch.results.length === 0 && (
                <LoadingSkeleton count={4} />
              )}
              {equipSearch.results.length > 0 && (
                <div className="space-y-1.5">
                  <p className="text-xs text-text-muted">
                    {equipSearch.results.length} result
                    {equipSearch.results.length !== 1 ? "s" : ""}
                  </p>
                  <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                    {equipSearch.results.map((equip, idx) => (
                      <div key={equip.equipmentId}>
                        <EquipmentSearchCard
                          equipment={equip}
                          isExpanded={expandedSearchIdx === idx}
                          onClick={() =>
                            setExpandedSearchIdx((prev) =>
                              prev === idx ? null : idx,
                            )
                          }
                        />
                        {/* Desktop inline expansion */}
                        {isDesktop && expandedSearchIdx === idx && (
                          <div className="col-span-full animate-expand-down rounded-lg border border-border bg-bg-surface p-4 mt-2">
                            <GearSlotExpandedContent
                              slot={{
                                slotName: equip.name,
                                slotKey: "Search",
                                recommendation: equip,
                                alternatives: [],
                                isEmpty: false,
                              }}
                              isDoH={equip.stats.craftsmanship > 0}
                              onClose={() => setExpandedSearchIdx(null)}
                            />
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Mobile bottom sheet */}
              {!isDesktop && expandedSearchSlot && (
                <BottomSheet onClose={() => setExpandedSearchIdx(null)}>
                  <GearSlotExpandedContent
                    slot={expandedSearchSlot}
                    isDoH={expandedIsDoH}
                    onClose={() => setExpandedSearchIdx(null)}
                  />
                </BottomSheet>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Apply gear dialog */}
      <ApplyGearDialog
        open={applyDialog.open}
        onClose={() => setApplyDialog((prev) => ({ ...prev, open: false }))}
        slots={applyDialog.slots}
        mode={applyDialog.mode}
        slotName={applyDialog.slotName}
        onSuccess={handleApplySuccess}
      />
    </div>
  );
}

export default function GearPage() {
  return (
    <Suspense fallback={<GearLoadingSkeleton />}>
      <GearPageContent />
    </Suspense>
  );
}
