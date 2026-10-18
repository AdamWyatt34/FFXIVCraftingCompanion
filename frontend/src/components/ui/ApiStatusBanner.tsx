"use client";

import { useState } from "react";
import { CloudOff, RefreshCw } from "lucide-react";
import { useApiAvailability } from "@/hooks/useApiAvailability";
import { probeApi } from "@/lib/api-status";

/**
 * Shown while the account API is unreachable (it runs on a free tier that pauses). Recipes, the
 * simulator, the solver and gear all come from the static data set and the WASM engine, so the
 * rest of the site keeps working and the banner says so.
 */
export function ApiStatusBanner() {
  const availability = useApiAvailability();
  const [isRetrying, setIsRetrying] = useState(false);

  if (availability !== "unavailable") return null;

  async function retry() {
    setIsRetrying(true);
    try {
      await probeApi();
    } finally {
      setIsRetrying(false);
    }
  }

  return (
    <div
      role="status"
      aria-live="polite"
      className="border-b border-amber-500/30 bg-amber-500/10 px-4 py-2 text-xs text-amber-200"
    >
      <div className="mx-auto flex max-w-4xl items-center gap-3">
        <CloudOff size={16} className="shrink-0 text-amber-400" aria-hidden="true" />
        <p className="flex-1">
          Accounts are paused right now: sign-in, saved lists, favourites and the rotation library
          can&apos;t be reached. Recipes, the simulator, the solver and gear work as usual.
        </p>
        <button
          type="button"
          onClick={retry}
          disabled={isRetrying}
          className="inline-flex shrink-0 items-center gap-1 rounded-md border border-amber-400/40 px-2 py-1 font-medium text-amber-200 hover:bg-amber-500/20 disabled:opacity-60"
        >
          <RefreshCw size={12} className={isRetrying ? "animate-spin" : ""} aria-hidden="true" />
          Retry
        </button>
      </div>
    </div>
  );
}
