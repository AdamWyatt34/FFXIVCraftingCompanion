/**
 * Which engine runs the simulator and solver: the WASM build of `crates/xiv-craft` in the
 * browser, or the .NET API. Resolution order: `localStorage["ffxiv_engine"]`, then
 * `NEXT_PUBLIC_ENGINE_MODE`, then `wasm`. Read per call so a change applies without a reload.
 *
 * `?engine=api` or `?engine=wasm` on any page writes the localStorage key, which is how the two
 * paths are compared side by side in production during the fallback release.
 */

export type EngineMode = "wasm" | "api";

export const ENGINE_MODE_STORAGE_KEY = "ffxiv_engine";
const QUERY_PARAM = "engine";

function isEngineMode(value: unknown): value is EngineMode {
  return value === "wasm" || value === "api";
}

function envDefault(): EngineMode {
  const configured = process.env.NEXT_PUBLIC_ENGINE_MODE;
  return isEngineMode(configured) ? configured : "wasm";
}

export function getEngineMode(): EngineMode {
  if (typeof window === "undefined") return envDefault();
  try {
    const stored = window.localStorage.getItem(ENGINE_MODE_STORAGE_KEY);
    if (isEngineMode(stored)) return stored;
  } catch {
    // Storage unavailable (private mode, blocked); fall through to the env default.
  }
  return envDefault();
}

export function setEngineMode(mode: EngineMode | null): void {
  try {
    if (mode === null) {
      window.localStorage.removeItem(ENGINE_MODE_STORAGE_KEY);
    } else {
      window.localStorage.setItem(ENGINE_MODE_STORAGE_KEY, mode);
    }
  } catch {
    // Storage unavailable; the env default stays in effect.
  }
}

/**
 * Applies `?engine=` from the current URL to localStorage. Call once on the client (the tabs
 * layout does); returns the mode that was applied, or null when the URL carries none.
 */
export function applyEngineModeFromUrl(): EngineMode | null {
  if (typeof window === "undefined") return null;
  const value = new URLSearchParams(window.location.search).get(QUERY_PARAM);
  if (!isEngineMode(value)) return null;
  setEngineMode(value);
  return value;
}
