import { useState, useEffect, useCallback, useRef } from "react";
import { getCraftingJourney, ApiClientError } from "@/lib/api-client";
import type { CraftingJourneyDto } from "@/types/journey";

interface UseJourneyReturn {
  journey: CraftingJourneyDto | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
}

export function useJourney(recipeId: number): UseJourneyReturn {
  const abortRef = useRef<AbortController | null>(null);
  const [retryCount, setRetryCount] = useState(0);
  const [journey, setJourney] = useState<CraftingJourneyDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isNaN(recipeId)) return;

    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    (async () => {
      setIsLoading(true);
      setError(null);
      try {
        const result = await getCraftingJourney(recipeId, controller.signal);
        if (!controller.signal.aborted) {
          setJourney(result);
          setIsLoading(false);
        }
      } catch (err: unknown) {
        if (controller.signal.aborted) return;
        if (err instanceof DOMException && err.name === "AbortError") return;
        const msg =
          err instanceof ApiClientError
            ? err.message
            : "Failed to load crafting journey.";
        setError(msg);
        setIsLoading(false);
      }
    })();

    return () => controller.abort();
  }, [recipeId, retryCount]);

  const retry = useCallback(() => {
    setJourney(null);
    setRetryCount((c) => c + 1);
  }, []);

  return { journey, isLoading, error, retry };
}
