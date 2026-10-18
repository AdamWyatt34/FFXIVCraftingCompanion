import * as Sentry from "@sentry/nextjs";

// Imported by src/instrumentation.ts for the edge runtime. Errors only: no tracing.
if (process.env.NODE_ENV === "production" && process.env.NEXT_PUBLIC_SENTRY_DSN) {
  Sentry.init({
    dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
    environment: process.env.VERCEL_ENV ?? process.env.NODE_ENV,
    sendDefaultPii: false,
  });
}
