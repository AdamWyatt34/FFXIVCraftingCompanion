"use client";

import { Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { ArrowLeft } from "lucide-react";

function BackLink() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const fromGear = searchParams.get("from") === "gear";

  return (
    <button
      onClick={() => (fromGear ? router.push("/gear") : router.back())}
      className="flex items-center gap-1 text-text-secondary hover:text-gold transition-colors text-sm"
    >
      <ArrowLeft size={16} />
      {fromGear ? "Back to Gear Browser" : "Back to search"}
    </button>
  );
}

/** "Back" at the top of a recipe page; reads the query string, so it hydrates after the HTML. */
export function RecipeBackLink() {
  return (
    <Suspense fallback={<div className="h-5" aria-hidden="true" />}>
      <BackLink />
    </Suspense>
  );
}
