"use client";

import { useEffect } from "react";
import { applyEngineModeFromUrl } from "@/lib/engine-mode";

/**
 * Persists `?engine=api|wasm` from the URL into localStorage on first render, so one browser
 * can be switched between the local engine and the API for a side-by-side comparison without
 * a redeploy. Renders nothing.
 */
export function EngineModeFromUrl() {
  useEffect(() => {
    applyEngineModeFromUrl();
  }, []);
  return null;
}
