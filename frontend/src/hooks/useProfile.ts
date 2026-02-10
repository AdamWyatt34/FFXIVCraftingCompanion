"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import { getProfile, ApiClientError } from "@/lib/api-client";
import { useAuth } from "@/contexts/AuthContext";
import type { CharacterProfileDto } from "@/types/profile";

let cachedProfile: CharacterProfileDto | null = null;

interface UseProfileReturn {
  profile: CharacterProfileDto | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
  mutate: (profile: CharacterProfileDto) => void;
  invalidate: () => void;
}

export function useProfile(): UseProfileReturn {
  const { isAuthenticated, isLoading: authLoading } = useAuth();
  const [profile, setProfile] = useState<CharacterProfileDto | null>(cachedProfile);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);
  const hasFetched = useRef(false);

  const fetchProfile = useCallback(async () => {
    if (!isAuthenticated) return;

    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    setIsLoading(true);
    setError(null);

    try {
      const data = await getProfile(controller.signal);
      cachedProfile = data;
      setProfile(data);
    } catch (err) {
      if (err instanceof DOMException && err.name === "AbortError") return;
      if (err instanceof ApiClientError && err.status === 404) {
        cachedProfile = null;
        setProfile(null);
      } else {
        setError(err instanceof ApiClientError ? err.body.error : "Failed to load profile");
      }
    } finally {
      setIsLoading(false);
    }
  }, [isAuthenticated]);

  useEffect(() => {
    if (authLoading) return;
    if (!isAuthenticated) {
      cachedProfile = null;
      setProfile(null);
      setIsLoading(false);
      return;
    }
    if (cachedProfile) {
      setProfile(cachedProfile);
      return;
    }
    if (!hasFetched.current) {
      hasFetched.current = true;
      fetchProfile();
    }
  }, [isAuthenticated, authLoading, fetchProfile]);

  useEffect(() => {
    return () => {
      abortRef.current?.abort();
    };
  }, []);

  const mutate = useCallback((p: CharacterProfileDto) => {
    cachedProfile = p;
    setProfile(p);
  }, []);

  const invalidate = useCallback(() => {
    cachedProfile = null;
    hasFetched.current = false;
    fetchProfile();
  }, [fetchProfile]);

  const retry = useCallback(() => {
    hasFetched.current = false;
    fetchProfile();
  }, [fetchProfile]);

  return { profile, isLoading: isLoading || authLoading, error, retry, mutate, invalidate };
}
