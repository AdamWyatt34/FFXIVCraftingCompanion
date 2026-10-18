// Port of FFXIV.Domain/Utilities/SpawnWindows.cs: the one reader of a gathering node's stored
// spawnTimes. Storage is a JSON array of "H:MM" strings in start/end pairs (["20:00","0:00"] is
// one window, ["2:00","4:00","10:00","12:00"] two). Data exported before the spawn-time fix holds
// raw game-data HHMM values ("2000:00" is 20:00) and the 65535 "no time" sentinel, which yields no
// window. Proven by goldens/spawn-windows.json.

export interface SpawnWindow {
  startHour: number;
  endHour: number;
}

/** The game data's "no value" for ephemeral and rare-pop times. */
export const NO_TIME = 65535;

/** Canonical storage form, or null when there are no windows. */
export function serializeSpawnWindows(windows: readonly SpawnWindow[]): string | null {
  if (windows.length === 0) return null;
  return JSON.stringify(windows.flatMap((w) => [`${w.startHour}:00`, `${w.endHour}:00`]));
}

/**
 * Parses stored spawn times. Never throws: anything unreadable, a sentinel pair, an hour outside
 * 0–23 or an empty window (start === end) is dropped. Order is preserved.
 */
export function parseSpawnWindows(spawnTimes: string | null | undefined): SpawnWindow[] {
  if (!spawnTimes || spawnTimes.trim().length === 0) return [];

  let root: unknown;
  try {
    root = JSON.parse(spawnTimes);
  } catch {
    return [];
  }
  if (!Array.isArray(root)) return [];

  const hours = root.map(hourFromElement);
  const windows: SpawnWindow[] = [];
  for (let i = 0; i + 1 < hours.length; i += 2) {
    const start = hours[i];
    const end = hours[i + 1];
    if (start !== null && end !== null && start !== end) windows.push({ startHour: start, endHour: end });
  }
  return windows;
}

/**
 * The hour of a game-data time value: either a plain hour (0–23) or an HHMM value (2000 is
 * 20:00). Null for the sentinel and anything out of range. Minutes are dropped: windows are
 * evaluated on whole Eorzean hours.
 */
export function hourFromGameTime(value: number): number | null {
  if (!Number.isInteger(value) || value < 0 || value === NO_TIME) return null;
  if (value <= 23) return value;
  if (value < 100) return null;
  const hour = Math.floor(value / 100);
  const minute = value % 100;
  return hour <= 23 && minute <= 59 ? hour : null;
}

const DIGITS = /^[0-9]+$/;

function hourFromElement(element: unknown): number | null {
  if (typeof element === "number") return hourFromGameTime(element);
  if (typeof element !== "string") return null;
  const colon = element.indexOf(":");
  const hourText = (colon < 0 ? element : element.slice(0, colon)).trim();
  const minuteText = colon < 0 ? "0" : element.slice(colon + 1).trim();
  if (!DIGITS.test(hourText) || !DIGITS.test(minuteText)) return null;
  const minute = Number(minuteText);
  if (minute > 59) return null;
  return hourFromGameTime(Number(hourText));
}

const pad = (hour: number) => `${hour}:00`;

/** Display text such as "20:00–0:00 ET" or "2:00–4:00, 10:00–12:00 ET"; empty when there is no window. */
export function formatSpawnWindows(windows: readonly SpawnWindow[]): string {
  if (windows.length === 0) return "";
  return `${windows.map((w) => `${pad(w.startHour)}–${pad(w.endHour)}`).join(", ")} ET`;
}
