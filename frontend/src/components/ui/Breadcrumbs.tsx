"use client";

import Link from "next/link";
import { useBreadcrumbs } from "@/contexts/BreadcrumbContext";

export function Breadcrumbs() {
  const { breadcrumbs } = useBreadcrumbs();

  if (breadcrumbs.length === 0) return null;

  // On mobile: show only parent + current (last 2)
  // On desktop: show all
  const mobileCrumbs = breadcrumbs.length > 2 ? breadcrumbs.slice(-2) : breadcrumbs;

  return (
    <nav
      aria-label="Breadcrumb"
      className="px-4 py-1.5 bg-bg-secondary/50 border-b border-border text-xs"
    >
      {/* Desktop: full breadcrumb trail */}
      <ol className="hidden sm:flex items-center gap-1 text-text-muted">
        {breadcrumbs.map((crumb, i) => {
          const isLast = i === breadcrumbs.length - 1;
          return (
            <li key={i} className="flex items-center gap-1">
              {i > 0 && <span className="text-text-muted/50">&gt;</span>}
              {isLast || !crumb.href ? (
                <span aria-current={isLast ? "page" : undefined} className="text-text-secondary truncate max-w-[200px]">
                  {crumb.label}
                </span>
              ) : (
                <Link
                  href={crumb.href}
                  className="hover:text-gold transition-colors truncate max-w-[200px]"
                >
                  {crumb.label}
                </Link>
              )}
            </li>
          );
        })}
      </ol>

      {/* Mobile: condensed trail */}
      <ol className="flex sm:hidden items-center gap-1 text-text-muted">
        {mobileCrumbs.map((crumb, i) => {
          const isLast = i === mobileCrumbs.length - 1;
          return (
            <li key={i} className="flex items-center gap-1">
              {i > 0 && <span className="text-text-muted/50">&gt;</span>}
              {isLast || !crumb.href ? (
                <span aria-current={isLast ? "page" : undefined} className="text-text-secondary truncate max-w-[150px]">
                  {crumb.label}
                </span>
              ) : (
                <Link
                  href={crumb.href}
                  className="hover:text-gold transition-colors truncate max-w-[150px]"
                >
                  {crumb.label}
                </Link>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
