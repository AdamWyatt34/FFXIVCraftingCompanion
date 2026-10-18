// Response headers for every route, built once at config time (next.config.ts imports this).
// The Content Security Policy is report-only to start with: it is written from what the app is
// known to load, and violation reports go to Sentry so anything missed shows up before the
// policy is enforced. Nonces are not used because they would force every page to render
// dynamically; Next's own inline bootstrap scripts need 'unsafe-inline' instead.

export interface SecurityHeaderInputs {
  /** The account API's origin (NEXT_PUBLIC_API_URL); empty when it is proxied through the rewrite. */
  apiOrigin?: string | null;
  /** The Sentry DSN; its project turns into the CSP report endpoint. */
  sentryDsn?: string | null;
  /** Where the browser reads the game data: a CDN URL, or this deployment's own path (ignored). */
  dataBase?: string | null;
}

export interface Header {
  key: string;
  value: string;
}

/** Sentry's security-report endpoint for a DSN, or null when the DSN is unusable. */
export function sentryReportUri(dsn: string | null | undefined): string | null {
  if (!dsn) return null;
  try {
    const url = new URL(dsn);
    const projectId = url.pathname.replace(/^\/+/, "");
    if (!url.username || !projectId) return null;
    return `${url.protocol}//${url.host}/api/${projectId}/security/?sentry_key=${url.username}`;
  } catch {
    return null;
  }
}

function origin(value: string | null | undefined): string | null {
  if (!value) return null;
  try {
    return new URL(value).origin;
  } catch {
    return null;
  }
}

export function buildContentSecurityPolicy(inputs: SecurityHeaderInputs): string {
  const apiOrigin = origin(inputs.apiOrigin);
  const sentryOrigin = origin(inputs.sentryDsn);
  const dataOrigin = origin(inputs.dataBase);
  const reportUri = sentryReportUri(inputs.sentryDsn);

  const connect = [
    "'self'",
    apiOrigin,
    // The exported game data, when a CDN serves it rather than the deployment.
    dataOrigin,
    // Market prices are fetched from Universalis in the browser; icons come from XIVAPI.
    "https://universalis.app",
    "https://v2.xivapi.com",
    // Vercel Web Analytics beacons.
    "https://vitals.vercel-insights.com",
    sentryOrigin,
  ].filter((value): value is string => value !== null);

  const directives = [
    "default-src 'self'",
    // Next.js bootstraps with inline scripts; the solver and simulator run WebAssembly.
    "script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval'",
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob: https://v2.xivapi.com https://xivapi.com https://img2.finalfantasyxiv.com https://cdn.discordapp.com",
    "font-src 'self' data:",
    `connect-src ${[...new Set(connect)].join(" ")}`,
    "worker-src 'self' blob:",
    "manifest-src 'self'",
    "frame-ancestors 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "object-src 'none'",
  ];
  if (reportUri) directives.push(`report-uri ${reportUri}`);

  return directives.join("; ");
}

export function securityHeaders(inputs: SecurityHeaderInputs): Header[] {
  return [
    { key: "X-Content-Type-Options", value: "nosniff" },
    { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
    {
      key: "Permissions-Policy",
      value: "camera=(), microphone=(), geolocation=(), payment=(), usb=(), interest-cohort=()",
    },
    { key: "X-Frame-Options", value: "DENY" },
    { key: "Content-Security-Policy-Report-Only", value: buildContentSecurityPolicy(inputs) },
  ];
}

/** The pre-custom-domain host; anything reaching it is sent to the canonical site. */
export const LEGACY_VERCEL_HOST = "ffxiv-crafting-companion.vercel.app";

/** Escapes a hostname for the regular expression Next's `has: [{ type: "host" }]` matcher takes. */
export function hostPattern(host: string): string {
  return host.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}
