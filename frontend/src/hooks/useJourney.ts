import { useState, useEffect, useCallback, useRef } from "react";
import { ApiClientError } from "@/lib/api-client";
import { getCraftingJourney } from "@/lib/game-data";
import { wasmSufficiencyEngine } from "@/lib/sufficiency-engine";
import { viewerFromProfile } from "@/lib/static-data/sufficiency";
import { useProfile } from "@/hooks/useProfile";
import type { CraftingJourneyDto } from "@/types/journey";

interface UseJourneyReturn {
  journey: CraftingJourneyDto | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
}

/**
 * The journey for a recipe. A signed-out visitor gets it without the stat check; once the
 * character profile is known the journey is recomposed with the stat check, in place, without
 * returning to the loading state.
 */
export function useJourney(recipeId: number): UseJourneyReturn {
  const { profile } = useProfile();
  const abortRef = useRef<AbortController | null>(null);
  const hasJourney = useRef(false);
  const [retryCount, setRetryCount] = useState(0);
  const [journey, setJourney] = useState<CraftingJourneyDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isNaN(recipeId)) return;

    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    const viewer = profile ? viewerFromProfile(profile, wasmSufficiencyEngine) : null;

    (async () => {
      if (!hasJourney.current) setIsLoading(true);
      setError(null);
      try {
        const result = await getCraftingJourney(recipeId, controller.signal, viewer);
        if (!controller.signal.aborted) {
          hasJourney.current = true;
          setJourney(result);
          setIsLoading(false);
        }
      } catch (err: unknown) {
        if (controller.signal.aborted) return;
        const msg =
          err instanceof ApiClientError
            ? err.message
            : "Failed to load crafting journey.";
        setError(msg);
        setIsLoading(false);
      }
    })();

    return () => controller.abort();
  }, [recipeId, retryCount, profile]);

  const retry = useCallback(() => {
    hasJourney.current = false;
    setJourney(null);
    setRetryCount((c) => c + 1);
  }, []);

  return { journey, isLoading, error, retry };
}
