"use client";

import { useSyncExternalStore } from "react";
import {
  getApiAvailability,
  subscribeApiAvailability,
  type ApiAvailability,
} from "@/lib/api-status";

const getServerSnapshot = (): ApiAvailability => "unknown";

/** Whether the account API is reachable, as learned from the requests the app has made. */
export function useApiAvailability(): ApiAvailability {
  return useSyncExternalStore(subscribeApiAvailability, getApiAvailability, getServerSnapshot);
}
