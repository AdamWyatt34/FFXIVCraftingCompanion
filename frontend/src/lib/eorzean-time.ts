/** Eorzean time constants — must match Domain/Utilities/EorzeanTime.cs */
export const EORZEAN_HOUR_IN_SECONDS = 175.0;
export const EORZEAN_DAY_IN_SECONDS = 4200.0;

/** Get the current Eorzean hour (0–23). */
export function getCurrentEorzeanHour(): number {
  return getEorzeanHour(Date.now());
}

/** Get the Eorzean hour for a given Unix timestamp (ms). */
export function getEorzeanHour(timestampMs: number): number {
  const epochSeconds = timestampMs / 1000;
  const eorzeanTotalHours =
    (epochSeconds * (3600.0 / EORZEAN_HOUR_IN_SECONDS)) / 3600.0;
  return Math.floor(eorzeanTotalHours % 24);
}

/** Is a timed-node window currently active? */
export function isWindowActive(
  startHour: number,
  endHour: number,
  currentHour?: number,
): boolean {
  const hour = currentHour ?? getCurrentEorzeanHour();
  if (startHour === endHour) return false;
  if (startHour < endHour) return hour >= startHour && hour < endHour;
  // Midnight wrap
  return hour >= startHour || hour < endHour;
}

/**
 * Real-world seconds until a timed-node window opens.
 * Returns 0 if the window is already active.
 */
export function timeUntilWindowSeconds(
  startHour: number,
  endHour: number,
  currentHour?: number,
): number {
  const hour = currentHour ?? getCurrentEorzeanHour();
  if (isWindowActive(startHour, endHour, hour)) return 0;
  const hoursUntil = ((startHour - hour + 24) % 24);
  return hoursUntil * EORZEAN_HOUR_IN_SECONDS;
}

/** Format seconds into a human-readable countdown string. */
export function formatCountdown(totalSeconds: number): string {
  if (totalSeconds <= 0) return "now";
  const m = Math.floor(totalSeconds / 60);
  const s = Math.floor(totalSeconds % 60);
  if (m === 0) return `${s}s`;
  if (m < 60) return `${m}m ${s}s`;
  const h = Math.floor(m / 60);
  const rm = m % 60;
  return `${h}h ${rm}m`;
}
