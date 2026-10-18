/**
 * The site's canonical origin. Every page's canonical URL, the sitemap and the social images are
 * built from it, so a production build without it would publish the wrong URLs; previews and
 * local builds fall back to localhost.
 */
const PRODUCTION_FALLBACK_ERROR =
  "NEXT_PUBLIC_SITE_URL must be set for a production build: it is the origin every canonical URL, " +
  "sitemap entry and social image is published under.";

export function getSiteUrl(): string {
  const configured = process.env.NEXT_PUBLIC_SITE_URL;
  if (configured) return configured.replace(/\/+$/, "");
  if (process.env.VERCEL_ENV === "production") throw new Error(PRODUCTION_FALLBACK_ERROR);
  return "http://localhost:3000";
}
