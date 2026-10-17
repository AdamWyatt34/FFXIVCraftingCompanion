import { useCallback } from "react";
import { trackAnalyticsEvent } from "@/lib/analytics";

export function useAnalytics() {
  const track = useCallback(
    (event: string, metadata?: Record<string, unknown>) => trackAnalyticsEvent(event, metadata),
    [],
  );

  return { track };
}
