import { useCallback } from "react";
import { trackAnalyticsEvent } from "@/lib/api-client";

export function useAnalytics() {
  const track = useCallback(
    (event: string, metadata?: Record<string, unknown>) => {
      const pageUrl =
        typeof window !== "undefined" ? window.location.pathname : undefined;
      trackAnalyticsEvent(event, metadata, pageUrl);
    },
    [],
  );

  return { track };
}
