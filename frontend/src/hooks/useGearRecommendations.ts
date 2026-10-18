import { useState, useEffect, useRef, useCallback } from "react";
import { useDebounce } from "./useDebounce";
import {
  getGearRecommendations,
  getStatWeightProfiles,
  ApiClientError,
} from "@/lib/api-client";
import type { GearSetDto, StatWeightProfileDto } from "@/types/gear";
import { JOB_INFO, type GearJob } from "@/lib/gear-constants";

interface UseGearRecommendationsReturn {
  job: string | null;
  setJob: (job: string | null) => void;
  level: number;
  setLevel: (level: number) => void;
  profile: string;
  setProfile: (profile: string) => void;
  craftedOnly: boolean;
  setCraftedOnly: (v: boolean) => void;
  profiles: StatWeightProfileDto[];
  isLoadingProfiles: boolean;
  data: GearSetDto | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
}

const profileCache = new Map<string, StatWeightProfileDto[]>();
const recCache = new Map<string, GearSetDto>();

interface UseGearRecommendationsOptions {
  initialJob?: string | null;
  initialLevel?: number;
  initialProfile?: string;
  jobLevels?: Record<string, { level: number }> | null;
}

export function useGearRecommendations(
  options?: UseGearRecommendationsOptions,
): UseGearRecommendationsReturn {
  const [job, setJob] = useState<string | null>(options?.initialJob ?? null);
  const [level, setLevel] = useState(options?.initialLevel ?? 1);
  const [profile, setProfile] = useState(options?.initialProfile ?? "Balanced");
  const [craftedOnly, setCraftedOnly] = useState(false);
  const [retryCount, setRetryCount] = useState(0);

  const debouncedLevel = useDebounce(level, 500);

  // Profile fetching state
  const profileAbortRef = useRef<AbortController | null>(null);
  const [profileResult, setProfileResult] = useState<{
    profiles: StatWeightProfileDto[];
    error: string | null;
    key: string;
  }>({ profiles: [], error: null, key: "" });

  const profileKey = job ?? "";
  const cachedProfiles = profileKey ? (profileCache.get(profileKey) ?? null) : null;
  const profiles = cachedProfiles ?? (profileResult.key === profileKey ? profileResult.profiles : []);
  const isLoadingProfiles = !cachedProfiles && profileResult.key !== profileKey && !!profileKey;

  // Fetch profiles when job changes
  useEffect(() => {
    if (!profileKey || profileCache.has(profileKey)) return;

    profileAbortRef.current?.abort();
    const controller = new AbortController();
    profileAbortRef.current = controller;

    getStatWeightProfiles(profileKey, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        profileCache.set(profileKey, result);
        setProfileResult({ profiles: result, error: null, key: profileKey });
        // Auto-select first profile if current is invalid
        if (result.length > 0 && !result.some((p) => p.name === profile)) {
          setProfile(result[0].name);
        }
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setProfileResult({ profiles: [], error: "Failed to load profiles", key: profileKey });
      });

    return () => controller.abort();
  }, [profileKey]); // eslint-disable-line react-hooks/exhaustive-deps

  // Recommendations fetching state
  const recAbortRef = useRef<AbortController | null>(null);
  const [recResult, setRecResult] = useState<{
    data: GearSetDto | null;
    error: string | null;
    key: string;
  }>({ data: null, error: null, key: "" });

  const recKey = job && debouncedLevel >= 1 ? `${job}:${debouncedLevel}:${profile}:${craftedOnly}` : "";
  const cachedRec = recKey ? (recCache.get(recKey) ?? null) : null;
  const isLoading = !cachedRec && recResult.key !== recKey && !!recKey;
  // Stale-while-revalidate: keep previous results visible while loading new ones
  const data = cachedRec ?? (recResult.key === recKey ? recResult.data : (isLoading ? recResult.data : null));
  const error = cachedRec ? null : (recResult.key === recKey ? recResult.error : null);

  // Fetch recommendations when inputs change
  useEffect(() => {
    if (!recKey || recCache.has(recKey)) return;
    if (!job || debouncedLevel < 1) return;

    recAbortRef.current?.abort();
    const controller = new AbortController();
    recAbortRef.current = controller;

    getGearRecommendations(job, debouncedLevel, profile, controller.signal, craftedOnly)
      .then((result) => {
        if (controller.signal.aborted) return;
        recCache.set(recKey, result);
        setRecResult({ data: result, error: null, key: recKey });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const msg =
          err instanceof ApiClientError
            ? err.message
            : "Failed to load gear recommendations. Please try again.";
        setRecResult({ data: null, error: msg, key: recKey });
      });

    return () => controller.abort();
  }, [recKey, job, debouncedLevel, profile, craftedOnly, retryCount]);

  const retry = useCallback(() => {
    if (recKey) recCache.delete(recKey);
    setRecResult({ data: null, error: null, key: "" });
    setRetryCount((c) => c + 1);
  }, [recKey]);

  // When job changes, auto-set profile and level from user's character data
  const jobLevels = options?.jobLevels;
  const handleSetJob = useCallback((newJob: string | null) => {
    setJob(newJob);
    // Clear stale display — show loading spinner instead of old job's gear
    setRecResult({ data: null, error: null, key: "" });
    if (newJob) {
      const info = JOB_INFO[newJob as GearJob];
      if (info) {
        const defaultProfile = info.category === "DoL" ? "GatheringBalanced" : "Balanced";
        setProfile(defaultProfile);
      }
      const stored = jobLevels?.[newJob];
      if (stored) {
        setLevel(stored.level);
      }
    }
  }, [jobLevels]);

  return {
    job,
    setJob: handleSetJob,
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
  };
}
