"use client";

import { useEffect } from "react";
import { useBreadcrumbs, type BreadcrumbItem } from "@/contexts/BreadcrumbContext";

/** Lets a server-rendered page set the header breadcrumbs; renders nothing. */
export function SetBreadcrumbs({ items }: { items: BreadcrumbItem[] }) {
  const { setBreadcrumbs } = useBreadcrumbs();
  const serialized = JSON.stringify(items);
  useEffect(() => {
    setBreadcrumbs(JSON.parse(serialized) as BreadcrumbItem[]);
  }, [serialized, setBreadcrumbs]);
  return null;
}
