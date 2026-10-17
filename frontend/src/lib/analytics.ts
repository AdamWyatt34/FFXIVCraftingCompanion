// Vercel Web Analytics through its script tag (the npm package's peer range clashes with
// vitest's vite). Events queue in window.vaq until /_vercel/insights/script.js loads; outside
// Vercel the script 404s and the queue is simply never drained.

declare global {
  interface Window {
    va?: (event: "event" | "beforeSend" | "pageview", properties?: unknown) => void;
    vaq?: unknown[][];
  }
}

type EventValue = string | number | boolean | null;

/** Custom events take flat primitive properties; nested metadata is JSON-encoded per key. */
function flatten(metadata: Record<string, unknown> | undefined): Record<string, EventValue> {
  const data: Record<string, EventValue> = {};
  for (const [key, value] of Object.entries(metadata ?? {})) {
    if (value === undefined) continue;
    data[key] = value === null || ["string", "number", "boolean"].includes(typeof value)
      ? (value as EventValue)
      : JSON.stringify(value);
  }
  return data;
}

export function trackAnalyticsEvent(event: string, metadata?: Record<string, unknown>): void {
  if (typeof window === "undefined") return;
  try {
    window.va = window.va ?? ((...args: unknown[]) => { (window.vaq = window.vaq ?? []).push(args); });
    window.va("event", { name: event, data: flatten(metadata) });
  } catch {
    // Never let analytics break a page.
  }
}
