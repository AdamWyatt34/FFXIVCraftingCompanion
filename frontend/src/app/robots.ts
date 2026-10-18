import type { MetadataRoute } from "next";
import { getSiteUrl } from "@/lib/site-url";
import { sitemapCount } from "./sitemap";

// Every sitemap chunk is listed: Next serves generateSitemaps() output at /sitemap/<id>.xml and
// nothing at /sitemap.xml, which is what robots.txt used to point at.
export default async function robots(): Promise<MetadataRoute.Robots> {
  const siteUrl = getSiteUrl();
  const chunks = await sitemapCount();
  return {
    rules: {
      userAgent: "*",
      allow: "/",
      disallow: ["/api/", "/auth/", "/profile", "/offline"],
    },
    sitemap: Array.from({ length: chunks }, (_, id) => `${siteUrl}/sitemap/${id}.xml`),
  };
}
