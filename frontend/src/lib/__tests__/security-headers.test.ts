import { describe, expect, it } from "vitest";
import {
  LEGACY_VERCEL_HOST,
  buildContentSecurityPolicy,
  hostPattern,
  securityHeaders,
  sentryReportUri,
} from "../security-headers";

describe("security headers", () => {
  const dsn = "https://abc123@o4506.ingest.us.sentry.io/4507";

  it("turns a Sentry DSN into its security-report endpoint", () => {
    expect(sentryReportUri(dsn)).toBe("https://o4506.ingest.us.sentry.io/api/4507/security/?sentry_key=abc123");
    expect(sentryReportUri(undefined)).toBeNull();
    expect(sentryReportUri("")).toBeNull();
    expect(sentryReportUri("not a url")).toBeNull();
    expect(sentryReportUri("https://o1.ingest.sentry.io/")).toBeNull();
  });

  it("allows what the app loads and reports to Sentry", () => {
    const csp = buildContentSecurityPolicy({ apiOrigin: "https://api.example.com/", sentryDsn: dsn });
    expect(csp).toContain("default-src 'self'");
    expect(csp).toContain("script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval'");
    expect(csp).toMatch(/connect-src [^;]*https:\/\/api\.example\.com/);
    expect(csp).toMatch(/connect-src [^;]*https:\/\/universalis\.app/);
    expect(csp).toMatch(/connect-src [^;]*https:\/\/o4506\.ingest\.us\.sentry\.io/);
    expect(csp).toMatch(/img-src [^;]*https:\/\/v2\.xivapi\.com/);
    expect(csp).toContain("frame-ancestors 'none'");
    expect(csp).toContain("worker-src 'self' blob:");
    expect(csp.endsWith("report-uri https://o4506.ingest.us.sentry.io/api/4507/security/?sentry_key=abc123")).toBe(true);
  });

  it("works without an API origin or a DSN", () => {
    const csp = buildContentSecurityPolicy({});
    expect(csp).toContain("connect-src 'self' https://universalis.app");
    expect(csp).not.toContain("report-uri");
    expect(csp).not.toContain("null");
  });

  it("sets the five hardening headers", () => {
    const keys = securityHeaders({}).map((h) => h.key);
    expect(keys).toEqual([
      "X-Content-Type-Options",
      "Referrer-Policy",
      "Permissions-Policy",
      "X-Frame-Options",
      "Content-Security-Policy-Report-Only",
    ]);
  });

  it("escapes the legacy host for the redirect matcher", () => {
    expect(hostPattern(LEGACY_VERCEL_HOST)).toBe("ffxiv-crafting-companion\\.vercel\\.app");
    expect(new RegExp(`^${hostPattern(LEGACY_VERCEL_HOST)}$`).test("ffxiv-crafting-companionXvercel.app")).toBe(false);
  });
});
