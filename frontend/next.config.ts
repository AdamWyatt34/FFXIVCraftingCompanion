import { withSentryConfig } from "@sentry/nextjs";
import withSerwistInit from "@serwist/next";
import withBundleAnalyzerInit from "@next/bundle-analyzer";
import type { NextConfig } from "next";

const withSerwist = withSerwistInit({
  swSrc: "src/sw.ts",
  swDest: "public/sw.js",
  disable: process.env.NODE_ENV === "development",
});

const withBundleAnalyzer = withBundleAnalyzerInit({
  enabled: process.env.ANALYZE === "true",
});

const nextConfig: NextConfig = {
  reactStrictMode: true,
  turbopack: {},
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
