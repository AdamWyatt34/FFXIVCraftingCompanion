"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import { getNotifications, markNotificationRead, markAllNotificationsRead } from "@/lib/api-client";
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

export function useNotifications(): UseNotificationsReturn {
  const { isAuthenticated } = useAuth();
  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const dismissedRef = useRef(getDismissedSet());

  const fetchNotifications = useCallback(async () => {
    if (!isAuthenticated) return;
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

  // Initial fetch + polling
  useEffect(() => {
    if (!isAuthenticated) {
      setNotifications([]);
      return;
    }

    fetchNotifications();

    const poll = () => {
      // Pause when tab hidden or offline
      if (document.visibilityState === "hidden" || !navigator.onLine) return;
      fetchNotifications();
    };

    intervalRef.current = setInterval(poll, NOTIFICATION_CONSTANTS.POLL_INTERVAL_MS);

    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [isAuthenticated, fetchNotifications]);

  // Pause/resume on visibility change
  useEffect(() => {
    const handleVisibility = () => {
      if (document.visibilityState === "visible" && isAuthenticated) {
        fetchNotifications();
      }
    };
    document.addEventListener("visibilitychange", handleVisibility);
    return () => document.removeEventListener("visibilitychange", handleVisibility);
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
