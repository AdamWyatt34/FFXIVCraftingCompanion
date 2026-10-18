"use client";

import { Suspense, useState, useEffect } from "react";
import { useSearchParams } from "next/navigation";
import { User, FlaskConical, SlidersHorizontal, ClipboardList, Hammer } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useGearRecommendations } from "@/hooks/useGearRecommendations";
import { useGearComparison } from "@/hooks/useGearComparison";
import { useEquipmentSearch } from "@/hooks/useEquipmentSearch";
import { useProfile } from "@/hooks/useProfile";
import { useAuth } from "@/contexts/AuthContext";
import { useCraftPlanContext } from "@/contexts/CraftPlanContext";
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
import { useBreadcrumbs } from "@/contexts/BreadcrumbContext";
import type { GearSlotDto } from "@/types/gear";

function GearPageContent() {
  const searchParams = useSearchParams();

  // Auth & profile
  const { isAuthenticated } = useAuth();
  const { profile: userProfile, invalidate: invalidateProfile } = useProfile();
  const { clearPlan, addToPlan } = useCraftPlanContext();

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
    craftedOnly,
    setCraftedOnly,
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

  const router = useRouter();
  const { setBreadcrumbs } = useBreadcrumbs();
  useEffect(() => {
    setBreadcrumbs([{ label: "Gear" }]);
  }, [setBreadcrumbs]);

  const equipSearch = useEquipmentSearch(urlSearch ?? "");
  const isDesktop = useMediaQuery("(min-width: 640px)");

  // Search result expansion
  const [expandedSearchIdx, setExpandedSearchIdx] = useState<number | null>(null);

  const jobInfo = job ? JOB_INFO[job as GearJob] : null;
  const jobCategory = jobInfo?.category;
  const isDoH = jobCategory === "DoH";

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

  const handlePlanFullSet = () => {
    if (!data) return;
    const craftableItems = data.slots
      .filter((s) => s.recommendation?.source?.hasRecipe && s.recommendation?.source?.recipeId)
      .map((s) => ({
        recipeId: s.recommendation!.source!.recipeId!,
        itemName: s.recommendation!.name,
        iconUrl: s.recommendation!.iconUrl,
        craftType: "",
        recipeLevel: 0,
        quantity: 1,
      }));
    if (craftableItems.length === 0) return;
    // Through the context, so the plan page (and its badge) see the items; a raw sessionStorage
    // write was only read on the next full page load.
    clearPlan();
    for (const { quantity: _quantity, ...item } of craftableItems) addToPlan(item);
    router.push("/recipes/plan");
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
    <div className="flex flex-col flex-1 min-h-0">
      {/* Main content (the page heading is rendered by the server wrapper) */}
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
                  Select a job to see gear recommendations
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
                    <button
                      onClick={() => setCraftedOnly(!craftedOnly)}
                      className={`inline-flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium transition-colors border shrink-0 ${
                        craftedOnly
                          ? "bg-gold/20 text-gold border-gold/40"
                          : "bg-bg-elevated text-text-secondary border-border hover:border-gold/50 hover:text-text-primary"
                      }`}
                      title="Show only craftable equipment"
                    >
                      <Hammer size={14} />
                      Crafted Only
                    </button>
                  </div>
                </div>
              )}

              {/* Step 3: Recommendations */}
              {isLoading && <GearLoadingSkeleton />}
              {error && <ErrorState message={error} onRetry={retry} />}
              {data && (
                <div className="space-y-4 animate-slide-up">
                  <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-3">
                    <div className="w-full sm:flex-1 min-w-0">
                      <StatsSummary
                        stats={data.totalStats}
                        isDoH={isDoH}
                        jobCategory={jobCategory}
                        profileName={data.profile.displayName}
                      />
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      <SaveGearSetButton gearSet={data} />
                      <GearShareButton
                        job={data.job}
                        level={data.level}
                        profile={data.profile.name}
                        gearSet={data}
                      />
                      {isDoH && (
                        <>
                          <Link
                            href={`/simulator?craftsmanship=${data.totalStats.craftsmanship}&control=${data.totalStats.control}&cp=${data.totalStats.cp}&level=${data.level}`}
                            className="p-2 rounded-lg border border-border text-text-secondary hover:text-gold hover:border-gold transition-colors"
                            title="Test rotation with these stats"
                          >
                            <FlaskConical size={16} />
                          </Link>
                          <Link
                            href={`/simulator?craftsmanship=${data.totalStats.craftsmanship}&control=${data.totalStats.control}&cp=${data.totalStats.cp}&level=${data.level}&whatif=true`}
                            className="p-2 rounded-lg border border-border text-text-secondary hover:text-gold hover:border-gold transition-colors"
                            title="Explore stat impact on crafting"
                          >
                            <SlidersHorizontal size={16} />
                          </Link>
                        </>
                      )}
                      <button
                        onClick={handlePlanFullSet}
                        className="p-2 rounded-lg border border-border text-text-secondary hover:text-gold hover:border-gold transition-colors"
                        title="Plan full set crafting"
                      >
                        <ClipboardList size={16} />
                      </button>
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
                (equipSearch.query.length >= 2 || equipSearch.jobFilter !== null || equipSearch.slotFilter !== null) &&
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
