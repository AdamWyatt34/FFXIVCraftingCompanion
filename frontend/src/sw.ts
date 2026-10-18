/// <reference lib="webworker" />

import type { PrecacheEntry, SerwistPlugin } from "serwist";
import {
  Serwist,
  CacheFirst,
  NetworkFirst,
  ExpirationPlugin,
  CacheableResponsePlugin,
} from "serwist";

declare const self: ServiceWorkerGlobalScope & {
  __SW_MANIFEST: (PrecacheEntry | string)[];
};

/** The CDN base of the game data when the build found it there; the deployment's own path otherwise. */
const DATA_BASE_URL = /^https?:\/\//.test(process.env.NEXT_PUBLIC_DATA_BASE ?? "")
  ? (process.env.NEXT_PUBLIC_DATA_BASE as string)
  : null;

const cacheableResponse: SerwistPlugin = new CacheableResponsePlugin({
  statuses: [0, 200],
});

const runtimeCaching = [
  // Pages and their RSC payloads — network-first, so a page visited online opens offline
  // (the simulator and solver then run locally from the precached WASM engine). Never-visited
  // pages still fall through to the /offline fallback below.
  {
    matcher: ({ request }: { request: Request }) =>
      request.mode === "navigate" || request.headers.get("RSC") === "1",
    handler: new NetworkFirst({
      cacheName: "pages",
      networkTimeoutSeconds: 5,
      plugins: [
        cacheableResponse,
        new ExpirationPlugin({
          maxEntries: 100,
          maxAgeSeconds: 7 * 24 * 60 * 60,
        }),
      ],
    }),
  },
  // Exported game data — versioned, immutable: cache-first, 30 day TTL. Served by the deployment
  // under /data/v/, or by the CDN the build pointed the site at (see scripts/data-fetch.mjs).
  {
    matcher: ({ request, url }: { request: Request; url: URL }) =>
      request.destination === "" &&
      ((url.origin === self.location.origin && url.pathname.startsWith("/data/v/")) ||
        (DATA_BASE_URL !== null && url.href.startsWith(`${DATA_BASE_URL}/`))),
    handler: new CacheFirst({
      cacheName: "game-data",
      plugins: [
        cacheableResponse,
        new ExpirationPlugin({
          maxEntries: 2000,
          maxAgeSeconds: 30 * 24 * 60 * 60,
        }),
      ],
    }),
  },
  // XIVAPI icon assets — cache-first, 30 day TTL
  {
    matcher: ({ url }: { url: URL }) =>
      url.hostname === "v2.xivapi.com" &&
      url.pathname.startsWith("/api/asset/"),
    handler: new CacheFirst({
      cacheName: "xivapi-icons",
      plugins: [
        cacheableResponse,
        new ExpirationPlugin({
          maxEntries: 10000,
          maxAgeSeconds: 30 * 24 * 60 * 60,
        }),
      ],
    }),
  },
];

const serwist = new Serwist({
  precacheEntries: self.__SW_MANIFEST,
  skipWaiting: true,
  clientsClaim: true,
  navigationPreload: true,
  runtimeCaching,
  fallbacks: {
    entries: [
      {
        url: "/offline",
        matcher({ request }) {
          return request.destination === "document";
        },
      },
    ],
  },
});

serwist.addEventListeners();
