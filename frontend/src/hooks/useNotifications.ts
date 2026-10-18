"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import { getNotifications, markNotificationRead, markAllNotificationsRead } from "@/lib/api-client";
import { getApiAvailability } from "@/lib/api-status";
import { useAuth } from "@/contexts/AuthContext";
import { NOTIFICATION_CONSTANTS } from "@/lib/constants";
import type { NotificationDto } from "@/types/notifications";

const STORAGE_KEY = NOTIFICATION_CONSTANTS.STORAGE_KEY;

function getDismissedSet(): Set<string> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) return new Set(JSON.parse(raw) as string[]);
  } catch { /* ignore */ }
  return new Set();
}

function saveDismissedSet(ids: Set<string>) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify([...ids]));
  } catch { /* ignore */ }
}

export interface UseNotificationsReturn {
  notifications: NotificationDto[];
  unreadCount: number;
  dismiss: (id: string) => void;
  dismissAll: () => void;
  isLoading: boolean;
}

/**
 * Notifications for the signed-in user. Fetched on sign-in, when the tab becomes visible or
 * regains focus (at most once a minute), and every five minutes while the tab is visible; a
 * hidden tab polls nothing, and an API known to be down is not polled either.
 */
export function useNotifications(): UseNotificationsReturn {
  const { isAuthenticated } = useAuth();
  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const dismissedRef = useRef(getDismissedSet());
  const lastFetchedAtRef = useRef(0);

  const fetchNotifications = useCallback(async () => {
    if (!isAuthenticated) return;
    if (getApiAvailability() === "unavailable") return;
    lastFetchedAtRef.current = Date.now();
    try {
      setIsLoading(true);
      const all = await getNotifications();
      // Client-side dismiss filter as backup
      const dismissed = dismissedRef.current;
      const filtered = all.filter((n) => !dismissed.has(n.id));
      setNotifications(filtered);
    } catch {
      // Silently fail — notifications are non-critical
    } finally {
      setIsLoading(false);
    }
  }, [isAuthenticated]);

  useEffect(() => {
    if (!isAuthenticated) {
      setNotifications([]);
      return;
    }

    fetchNotifications();

    let timer: ReturnType<typeof setInterval> | null = null;
    const start = () => {
      if (timer) return;
      timer = setInterval(() => {
        if (navigator.onLine) fetchNotifications();
      }, NOTIFICATION_CONSTANTS.POLL_INTERVAL_MS);
    };
    const stop = () => {
      if (timer) clearInterval(timer);
      timer = null;
    };
    const refreshIfStale = () => {
      if (Date.now() - lastFetchedAtRef.current >= NOTIFICATION_CONSTANTS.FOCUS_REFRESH_MIN_MS) {
        fetchNotifications();
      }
    };
    const handleVisibility = () => {
      if (document.visibilityState === "visible") {
        refreshIfStale();
        start();
      } else {
        stop();
      }
    };

    if (document.visibilityState === "visible") start();
    document.addEventListener("visibilitychange", handleVisibility);
    window.addEventListener("focus", refreshIfStale);

    return () => {
      stop();
      document.removeEventListener("visibilitychange", handleVisibility);
      window.removeEventListener("focus", refreshIfStale);
    };
  }, [isAuthenticated, fetchNotifications]);

  const dismiss = useCallback((id: string) => {
    dismissedRef.current.add(id);
    saveDismissedSet(dismissedRef.current);
    setNotifications((prev) => prev.filter((n) => n.id !== id));
    markNotificationRead(id).catch(() => {});
  }, []);

  const dismissAll = useCallback(() => {
    setNotifications((prev) => {
      for (const n of prev) dismissedRef.current.add(n.id);
      saveDismissedSet(dismissedRef.current);
      return [];
    });
    markAllNotificationsRead().catch(() => {});
  }, []);

  return {
    notifications,
    unreadCount: notifications.length,
    dismiss,
    dismissAll,
    isLoading,
  };
}
