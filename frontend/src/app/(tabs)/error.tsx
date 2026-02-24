"use client";

import { useEffect } from "react";
import * as Sentry from "@sentry/nextjs";
import Link from "next/link";
import { AlertTriangle, RotateCcw, Home } from "lucide-react";

export default function TabsError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    Sentry.captureException(error);
  }, [error]);

  return (
    <div className="flex flex-col items-center justify-center min-h-[50vh] p-6 text-center">
      <AlertTriangle size={48} className="text-gold mb-4" />
      <h2 className="text-xl font-bold text-text-primary mb-2">
        Something went wrong
      </h2>
      <p className="text-sm text-text-secondary mb-6 max-w-md">
        An unexpected error occurred while loading this page. You can try again
        or head back to the home page.
      </p>
      <div className="flex gap-3">
        <button
          onClick={reset}
          className="flex items-center gap-1.5 px-4 py-2 rounded-lg bg-gold text-bg-primary font-medium text-sm hover:bg-gold/80 transition-colors"
        >
          <RotateCcw size={16} />
          Try Again
        </button>
        <Link
          href="/"
          className="flex items-center gap-1.5 px-4 py-2 rounded-lg border border-border text-text-primary font-medium text-sm hover:border-gold transition-colors"
        >
          <Home size={16} />
          Home
        </Link>
      </div>
    </div>
  );
}
