import { withSentryConfig } from "@sentry/nextjs/config";
import withSerwistInit from "@serwist/next";
import withBundleAnalyzerInit from "@next/bundle-analyzer";
import type { NextConfig } from "next";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { LEGACY_VERCEL_HOST, hostPattern, securityHeaders } from "./src/lib/security-headers";

// scripts/data-fetch.mjs (the prebuild step) records which data set it placed and where it is
// read from: `base` is what the browser prefixes to a data path (a CDN URL for a locked data set,
// or this deployment's /data/v/<version>), `dir` is where the build reads it from disk. The
// version is baked into the bundle so every data URL names exactly one export.
function currentDataSet(): { version: string; bucketSize: number; base: string; dir: string } | null {
  try {
    return JSON.parse(readFileSync(join(__dirname, ".data", "current.json"), "utf8"));
  } catch {
    return null;
  }
}
const dataSet = currentDataSet();

const withSerwist = withSerwistInit({
  swSrc: "src/sw.ts",
  swDest: "public/sw.js",
  disable: process.env.NODE_ENV === "development",
  // public/data (the exported game data, hundreds of MB) is cached at runtime as pages touch
  // it, never precached, and nothing else in public/ is worth a download on install: only the
  // manifest and the icons are listed (node-glob has no negation).
  globPublicPatterns: ["manifest.json", "icons/**/*"],
});

const withBundleAnalyzer = withBundleAnalyzerInit({
  enabled: process.env.ANALYZE === "true",
});

// The canonical origin. Unset for local and e2e builds, which then get no host redirect.
const siteUrl = process.env.NEXT_PUBLIC_SITE_URL ? new URL(process.env.NEXT_PUBLIC_SITE_URL) : null;

const nextConfig: NextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  turbopack: {},
  // The repo root has its own (unrelated) package-lock.json; without this Next guesses the
  // workspace root from it and warns on every build.
  outputFileTracingRoot: __dirname,
  // The server loader (src/lib/static-data/server.ts) reads the data set from disk during the
  // build and over HTTP inside a function, so it must not be traced into any function bundle:
  // with it, the recipe page's function was 283 MB uncompressed, over Vercel's 250 MB limit.
  outputFileTracingExcludes: {
    "*": ["./public/data/**", "./.data/**"],
  },
  env: {
    NEXT_PUBLIC_DATA_VERSION: dataSet?.version ?? "",
    NEXT_PUBLIC_DATA_BUCKET_SIZE: String(dataSet?.bucketSize ?? 100),
    NEXT_PUBLIC_DATA_BASE: dataSet?.base ?? "",
    FFXIV_DATA_LOCAL_DIR: dataSet?.dir ?? "",
  },
  async headers() {
    return [
      {
        source: "/:path*",
        headers: securityHeaders({
          apiOrigin: process.env.NEXT_PUBLIC_API_URL,
          sentryDsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
          dataBase: dataSet?.base,
        }),
      },
      {
        source: "/data/v/:version/:path*",
        headers: [{ key: "Cache-Control", value: "public, max-age=31536000, immutable" }],
      },
    ];
  },
  async redirects() {
    // The project's *.vercel.app host still serves the whole site, which gives search engines a
    // duplicate of every page. Send it to the canonical host; previews have their own hosts and
    // never match.
    if (!siteUrl || siteUrl.host === LEGACY_VERCEL_HOST) return [];
    return [
      {
        source: "/:path*",
        has: [{ type: "host" as const, value: hostPattern(LEGACY_VERCEL_HOST) }],
        destination: `${siteUrl.origin}/:path*`,
        permanent: true,
      },
    ];
  },
  images: {
    // Serve remote icons as-is. XIVAPI/Lodestone icons are already small PNGs, and
    // routing every icon size through Vercel Image Optimization exhausted the Hobby
    // plan's 5,000 transformations and paused the team.
    unoptimized: true,
    remotePatterns: [
      {
        protocol: "https",
        hostname: "v2.xivapi.com",
        pathname: "/api/asset/**",
      },
      {
        protocol: "https",
        hostname: "img2.finalfantasyxiv.com",
        pathname: "/**",
      },
      {
        protocol: "https",
        hostname: "cdn.discordapp.com",
        pathname: "/**",
      },
    ],
  },
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: `${process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5159"}/api/:path*`,
      },
    ];
  },
};

const baseConfig = withBundleAnalyzer(withSerwist(nextConfig));

export default process.env.NEXT_PUBLIC_SENTRY_DSN
  ? withSentryConfig(baseConfig, {
      silent: true,
      webpack: { treeshake: { removeDebugLogging: true } },
      // Errors only (see src/instrumentation-client.ts): the tracing and replay code paths are
      // never used, so they are stripped from the client bundle rather than shipped as dead weight.
      bundleSizeOptimizations: {
        excludeDebugStatements: true,
        excludeTracing: true,
        excludeReplayIframe: true,
        excludeReplayShadowDom: true,
        excludeReplayWorker: true,
      },
    })
  : baseConfig;
