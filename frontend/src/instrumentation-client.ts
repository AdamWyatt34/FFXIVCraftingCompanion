import * as Sentry from "@sentry/nextjs";

// Browser-side Sentry (this file replaces the deprecated sentry.client.config.ts). Errors only:
// no tracing and no replay, which also lets the build strip those code paths from the bundle.
// Initialised only in production builds that carry a DSN, so local development and previews
// without one send nothing.
if (process.env.NODE_ENV === "production" && process.env.NEXT_PUBLIC_SENTRY_DSN) {
  Sentry.init({
    dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
    environment: process.env.NEXT_PUBLIC_VERCEL_ENV ?? process.env.NODE_ENV,
    // v11 collects user info, cookies, headers, bodies and query strings unless told otherwise;
    // this keeps the SDK's v10 behaviour (the former `sendDefaultPii: false`).
    dataCollection: {
      userInfo: false,
      cookies: false,
      httpHeaders: {
        request: { deny: ["forwarded", "-ip", "remote-", "via", "-user"] },
        response: { deny: ["forwarded", "-ip", "remote-", "via", "-user"] },
      },
      httpBodies: [],
      urlQueryParams: { deny: ["forwarded", "-ip", "remote-", "via", "-user"] },
      genAI: { inputs: false, outputs: false },
      databaseQueryData: false,
      graphQL: { document: false, variables: false },
    },
  });
}

// Required by the SDK's App Router integration; with tracing stripped from the bundle it records
// nothing, but its absence is reported on every build.
export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;
