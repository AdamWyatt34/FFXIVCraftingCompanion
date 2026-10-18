/** The account API's origin. Empty in development: the Next.js rewrite proxies /api there. */
export function getApiBaseUrl(): string {
  if (typeof window !== "undefined" && process.env.NODE_ENV === "development") {
    return "";
  }
  return process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5159";
}

/** The API's own origin, rewrite or not; the liveness probe is not under /api. */
export function getApiOrigin(): string {
  return process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5159";
}
