import * as Sentry from "@sentry/nextjs";

// Server-side Sentry. Next.js runs register() once per runtime when the server starts; without
// this file the root sentry.*.config.ts files were never imported, so server errors never reached
// Sentry.
export async function register() {
  if (process.env.NEXT_RUNTIME === "nodejs") {
    await import("../sentry.server.config");
  }

  if (process.env.NEXT_RUNTIME === "edge") {
    await import("../sentry.edge.config");
  }
}

// Errors thrown while rendering server components or handling routes arrive through this hook.
export const onRequestError = Sentry.captureRequestError;
