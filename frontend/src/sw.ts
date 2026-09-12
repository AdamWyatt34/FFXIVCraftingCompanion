/// <reference lib="webworker" />

import type { PrecacheEntry, SerwistPlugin } from "serwist";
import {
  Serwist,
  CacheFirst,
  NetworkFirst,
  NetworkOnly,
  StaleWhileRevalidate,
  ExpirationPlugin,
  CacheableResponsePlugin,
} from "serwist";

declare const self: ServiceWorkerGlobalScope & {
  __SW_MANIFEST: (PrecacheEntry | string)[];
};

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
  // Game data APIs — stale-while-revalidate, 7 day TTL
  {
    matcher: ({ request, url }: { request: Request; url: URL }) =>
      request.destination === "" &&
      /\/api\/(items|recipes|gear|crafting\/actions)(\/|$|\?)/.test(
        url.pathname,
      ),
    handler: new StaleWhileRevalidate({
      cacheName: "game-data-api",
      plugins: [
        cacheableResponse,
        new ExpirationPlugin({
          maxEntries: 5000,
          maxAgeSeconds: 7 * 24 * 60 * 60,
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
  // Solver/simulate — never cache (results are unique per input)
  {
    matcher: ({ request, url }: { request: Request; url: URL }) =>
      request.destination === "" &&
      /\/api\/crafting\/(solve|simulate)/.test(url.pathname),
    handler: new NetworkOnly(),
  },
  // Market data — network-first, 15 min TTL
  {
    matcher: ({ request, url }: { request: Request; url: URL }) =>
      request.destination === "" && /\/api\/market(\/|$|\?)/.test(url.pathname),
    handler: new NetworkFirst({
      cacheName: "market-data",
      networkTimeoutSeconds: 5,
      plugins: [
        cacheableResponse,
        new ExpirationPlugin({ maxEntries: 500, maxAgeSeconds: 15 * 60 }),
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
