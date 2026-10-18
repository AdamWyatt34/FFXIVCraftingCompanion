import { getApiOrigin } from "./api-base-url";

// Whether the account API is reachable. The API runs on a free tier that pauses; while it is
// down, sign-in, saved lists, favourites and the rotation library fail, but everything served
// from the static data set and the WASM engine keeps working. The status is learned from the
// account requests the app makes anyway (the silent sign-in when a refresh token is stored, a
// sign-in attempt, an account page) and never from an unprompted probe, so anonymous visitors
// never cause a request to the API. Once a failure has been seen, the liveness endpoint (which
// has no database behind it) is probed once a minute until the API answers again.

export type ApiAvailability = "unknown" | "available" | "unavailable";

const REPROBE_MS = 60_000;
const PROBE_TIMEOUT_MS = 5_000;

const listeners = new Set<() => void>();
let status: ApiAvailability = "unknown";
let reprobeTimer: ReturnType<typeof setTimeout> | null = null;

export function getApiAvailability(): ApiAvailability {
  return status;
}

export function subscribeApiAvailability(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

function setStatus(next: ApiAvailability): void {
  if (status === next) return;
  status = next;
  for (const listener of listeners) listener();
}

/**
 * Called by the API client after every account request. Any answer, including a 4xx, means the
 * API is up; a network failure, a timeout or a gateway error (502/503/504, what the ingress
 * returns while the app is down) means it is not.
 */
export function reportApiOutcome(outcome: "answered" | "unreachable"): void {
  if (outcome === "answered") {
    setStatus("available");
    if (reprobeTimer) {
      clearTimeout(reprobeTimer);
      reprobeTimer = null;
    }
    return;
  }

  setStatus("unavailable");
  scheduleReprobe();
}

/** Classifies an HTTP status the API answered with. */
export function outcomeForStatus(httpStatus: number): "answered" | "unreachable" {
  return httpStatus === 502 || httpStatus === 503 || httpStatus === 504 ? "unreachable" : "answered";
}

/**
 * Asks the liveness endpoint whether the API is back. Only ever called after a failure has been
 * observed (automatically, or from the banner's retry), so a visitor who never touched the API
 * never triggers it.
 */
export async function probeApi(): Promise<boolean> {
  try {
    const response = await fetch(`${getApiOrigin()}/health/live`, {
      cache: "no-store",
      signal: AbortSignal.timeout(PROBE_TIMEOUT_MS),
    });
    reportApiOutcome(response.ok ? "answered" : "unreachable");
    return response.ok;
  } catch {
    reportApiOutcome("unreachable");
    return false;
  }
}

function scheduleReprobe(): void {
  if (reprobeTimer || typeof window === "undefined") return;
  reprobeTimer = setTimeout(async () => {
    reprobeTimer = null;
    if (status !== "unavailable") return;
    // A hidden tab waits for the next tick instead of probing in the background.
    if (document.visibilityState === "hidden") {
      scheduleReprobe();
      return;
    }
    await probeApi();
  }, REPROBE_MS);
}

/** Test hook: forget everything, including a pending re-probe. */
export function resetApiStatusForTests(): void {
  status = "unknown";
  if (reprobeTimer) {
    clearTimeout(reprobeTimer);
    reprobeTimer = null;
  }
  listeners.clear();
}
