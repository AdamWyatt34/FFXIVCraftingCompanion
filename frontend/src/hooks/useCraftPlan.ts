import { useState, useCallback } from "react";
import {
  resolveCraftPlan,
  createCraftPlan,
  getCraftPlans,
  getCraftPlanById,
  getResolvedCraftPlan,
  deleteCraftPlan as apiDeletePlan,
  exportCraftPlan,
  ApiClientError,
  } from "@/lib/api-client";
import { trackAnalyticsEvent } from "@/lib/analytics";
import type { CraftPlanItemInput, CraftPlanResultDto, CraftPlanDto, ExportResultDto } from "@/types/craft-plan";

interface UseCraftPlanReturn {
  result: CraftPlanResultDto | null;
  isResolving: boolean;
  resolveError: string | null;
  resolve: (items: CraftPlanItemInput[], signal?: AbortSignal) => Promise<void>;
  clearResult: () => void;
  savePlan: (name: string, items: CraftPlanItemInput[]) => Promise<CraftPlanDto | null>;
  savedPlans: CraftPlanDto[];
  isLoadingPlans: boolean;
  loadPlans: () => Promise<void>;
  loadPlan: (id: string) => Promise<{ plan: CraftPlanDto; resolved: CraftPlanResultDto } | null>;
  deletePlan: (id: string) => Promise<void>;
  doExport: (items: CraftPlanItemInput[], format: string) => Promise<ExportResultDto | null>;
  saveError: string | null;
}

export function useCraftPlan(): UseCraftPlanReturn {
  const [result, setResult] = useState<CraftPlanResultDto | null>(null);
  const [isResolving, setIsResolving] = useState(false);
  const [resolveError, setResolveError] = useState<string | null>(null);
  const [savedPlans, setSavedPlans] = useState<CraftPlanDto[]>([]);
  const [isLoadingPlans, setIsLoadingPlans] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  const resolve = useCallback(async (items: CraftPlanItemInput[], signal?: AbortSignal) => {
    if (items.length === 0) return;
    setIsResolving(true);
    setResolveError(null);
    try {
      const data = await resolveCraftPlan(items, signal);
      setResult(data);
    } catch (err) {
      if (signal?.aborted) return;
      const msg = err instanceof ApiClientError ? err.message : "Failed to resolve plan.";
      setResolveError(msg);
    } finally {
      setIsResolving(false);
    }
  }, []);

  const savePlan = useCallback(async (name: string, items: CraftPlanItemInput[]): Promise<CraftPlanDto | null> => {
    setSaveError(null);
    try {
      const plan = await createCraftPlan(name, items);
      setSavedPlans((prev) => [plan, ...prev]);
      trackAnalyticsEvent("craft_plan_create", { itemCount: items.length });
      return plan;
    } catch (err) {
      const msg = err instanceof ApiClientError ? err.message : "Failed to save plan.";
      setSaveError(msg);
      return null;
    }
  }, []);

  const loadPlans = useCallback(async () => {
    setIsLoadingPlans(true);
    try {
      const plans = await getCraftPlans();
      setSavedPlans(plans);
    } catch {
      // silent fail
    } finally {
      setIsLoadingPlans(false);
    }
  }, []);

  const loadPlan = useCallback(async (id: string) => {
    try {
      const [plan, resolved] = await Promise.all([
        getCraftPlanById(id),
        getResolvedCraftPlan(id),
      ]);
      setResult(resolved);
      return { plan, resolved };
    } catch {
      return null;
    }
  }, []);

  const deletePlan = useCallback(async (id: string) => {
    try {
      await apiDeletePlan(id);
      setSavedPlans((prev) => prev.filter((p) => p.id !== id));
    } catch {
      // silent fail
    }
  }, []);

  const doExport = useCallback(async (items: CraftPlanItemInput[], format: string): Promise<ExportResultDto | null> => {
    try {
      return await exportCraftPlan(items, format);
    } catch {
      return null;
    }
  }, []);

  const clearResult = useCallback(() => {
    setResult(null);
    setResolveError(null);
  }, []);

  return {
    result,
    isResolving,
    resolveError,
    resolve,
    clearResult,
    savePlan,
    savedPlans,
    isLoadingPlans,
    loadPlans,
    loadPlan,
    deletePlan,
    doExport,
    saveError,
  };
}
