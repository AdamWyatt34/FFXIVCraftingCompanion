"use client";

import { useState, useEffect, useRef, useCallback } from "react";
import { getRotationById, ApiClientError } from "@/lib/api-client";
import type { RotationDetail } from "@/types/solver";

export interface UseRotationDetailReturn {
  rotation: RotationDetail | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
}

export function useRotationDetail(id: number): UseRotationDetailReturn {
  const [rotation, setRotation] = useState<RotationDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const fetchRotation = useCallback(async () => {
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    setIsLoading(true);
    setError(null);

    try {
      const data = await getRotationById(id, controller.signal);
      if (controller.signal.aborted) return;
      setRotation(data);
    } catch (err) {
      if (err instanceof DOMException && err.name === "AbortError") return;
      if (err instanceof ApiClientError && err.status === 404) {
        setError("Rotation not found.");
      } else {
        setError(
          err instanceof ApiClientError
            ? err.body.error
            : "Failed to load rotation",
        );
      }
    } finally {
      setIsLoading(false);
    }
  }, [id]);

  useEffect(() => {
    fetchRotation();
    return () => {
      abortRef.current?.abort();
    };
  }, [fetchRotation]);

  return { rotation, isLoading, error, retry: fetchRotation };
}
