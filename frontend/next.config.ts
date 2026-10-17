import { withSentryConfig } from "@sentry/nextjs";
import withSerwistInit from "@serwist/next";
import withBundleAnalyzerInit from "@next/bundle-analyzer";
import type { NextConfig } from "next";
import { readFileSync } from "node:fs";
import { join } from "node:path";

// scripts/data-fetch.mjs (the prebuild step) records which data set it placed under public/data;
// the version is baked into the bundle so every data URL names exactly one export.
function currentDataSet(): { version: string; bucketSize: number } | null {
  try {
    return JSON.parse(readFileSync(join(__dirname, "public", "data", "current.json"), "utf8"));
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
  // it, never precached. node-glob has no negation, so list what is precached instead.
  globPublicPatterns: ["*", "icons/**/*"],
});

const withBundleAnalyzer = withBundleAnalyzerInit({
  enabled: process.env.ANALYZE === "true",
});

const nextConfig: NextConfig = {
  reactStrictMode: true,
  turbopack: {},
  env: {
    NEXT_PUBLIC_DATA_VERSION: dataSet?.version ?? "",
    NEXT_PUBLIC_DATA_BUCKET_SIZE: String(dataSet?.bucketSize ?? 100),
  },
  async headers() {
    return [
      {
        source: "/data/v/:version/:path*",
        headers: [{ key: "Cache-Control", value: "public, max-age=31536000, immutable" }],
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
      disableLogger: true,
    })
  : baseConfig;
