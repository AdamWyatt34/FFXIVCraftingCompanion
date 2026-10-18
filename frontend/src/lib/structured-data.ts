import { getSiteUrl } from "@/lib/site-url";

/**
 * A schema.org BreadcrumbList for a page, as the JSON to put in a <script type="application/ld+json">.
 * Paths are site-relative; "<" is escaped so no name can close the script element.
 */
export function breadcrumbListJsonLd(items: { name: string; path: string }[]): string {
  const base = getSiteUrl();
  const list = {
    "@context": "https://schema.org",
    "@type": "BreadcrumbList",
    itemListElement: items.map((item, index) => ({
      "@type": "ListItem",
      position: index + 1,
      name: item.name,
      item: `${base}${item.path}`,
    })),
  };
  return JSON.stringify(list).replace(/</g, "\\u003c");
}
