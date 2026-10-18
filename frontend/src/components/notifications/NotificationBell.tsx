"use client";

import { useState, useRef, useEffect } from "react";
import { useFocusTrap } from "@/hooks/useFocusTrap";
import { Bell, Clock, Coins, X, CheckCheck } from "lucide-react";
import Link from "next/link";
import { useNotifications } from "@/hooks/useNotifications";
import type { NotificationDto, NotificationType } from "@/types/notifications";

const TYPE_ICONS: Record<NotificationType, typeof Bell> = {
  TimedNode: Clock,
  ScripReset: Coins,
  PriceAlert: Coins,
  PlanUpdate: Bell,
  General: Bell,
};

const PRIORITY_STYLES: Record<string, string> = {
  High: "border-l-red-400",
  Medium: "border-l-amber-400",
  Low: "border-l-text-muted",
};

function timeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60_000);
  if (mins < 1) return "just now";
  if (mins < 60) return `${mins}m ago`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
}

export function NotificationBell() {
  const { notifications, unreadCount, dismiss, dismissAll } = useNotifications();
  const [isOpen, setIsOpen] = useState(false);
  const mobileRef = useRef<HTMLDivElement>(null);
  useFocusTrap(mobileRef, isOpen);
  const panelRef = useRef<HTMLDivElement>(null);

  // Click outside to close
  useEffect(() => {
    if (!isOpen) return;
    const handler = (e: MouseEvent) => {
      if (panelRef.current && !panelRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, [isOpen]);

  return (
    <div className="relative" ref={panelRef}>
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="relative p-2 rounded-lg text-text-secondary hover:text-gold hover:bg-bg-elevated transition-colors"
        aria-label={`Notifications${unreadCount > 0 ? ` (${unreadCount} unread)` : ""}`}
      >
        <Bell size={20} />
        {unreadCount > 0 && (
          <span className="absolute -top-0.5 -right-0.5 min-w-[18px] h-[18px] flex items-center justify-center rounded-full bg-red-500 text-[10px] font-bold text-white px-1">
            {unreadCount > 99 ? "99+" : unreadCount}
          </span>
        )}
      </button>

      {isOpen && (
        <>
          {/* Mobile: full-screen overlay */}
          <div
            ref={mobileRef}
            role="dialog"
            aria-modal="true"
            aria-labelledby="notifications-title"
            className="sm:hidden fixed inset-0 z-50 bg-bg-primary"
          >
            <div className="flex items-center justify-between px-4 py-3 border-b border-border">
              <h2 id="notifications-title" className="text-sm font-semibold text-text-primary">Notifications</h2>
              <div className="flex items-center gap-2">
                {notifications.length > 0 && (
                  <button
                    onClick={dismissAll}
                    className="text-xs text-text-muted hover:text-gold transition-colors flex items-center gap-1"
                  >
                    <CheckCheck size={14} />
                    Mark all read
                  </button>
                )}
                <button
                  type="button"
                  onClick={() => setIsOpen(false)}
                  aria-label="Close notifications"
                  className="p-1 text-text-muted hover:text-text-primary"
                >
                  <X size={18} />
                </button>
              </div>
            </div>
            <div className="overflow-y-auto max-h-[calc(100vh-56px)]">
              <NotificationList
                notifications={notifications}
                onDismiss={dismiss}
                onClose={() => setIsOpen(false)}
              />
            </div>
          </div>

          {/* Desktop: dropdown */}
          <div className="hidden sm:block absolute right-0 top-full mt-2 w-80 max-h-[420px] bg-bg-surface border border-border rounded-lg shadow-lg overflow-hidden z-50">
            <div className="flex items-center justify-between px-3 py-2 border-b border-border bg-bg-elevated/50">
              <span className="text-xs font-semibold text-text-secondary uppercase tracking-wider">
                Notifications
              </span>
              {notifications.length > 0 && (
                <button
                  onClick={dismissAll}
                  className="text-xs text-text-muted hover:text-gold transition-colors flex items-center gap-1"
                >
                  <CheckCheck size={12} />
                  Mark all read
                </button>
              )}
            </div>
            <div className="overflow-y-auto max-h-[360px]">
              <NotificationList
                notifications={notifications}
                onDismiss={dismiss}
                onClose={() => setIsOpen(false)}
              />
            </div>
          </div>
        </>
      )}
    </div>
  );
}

function NotificationList({
  notifications,
  onDismiss,
  onClose,
}: {
  notifications: NotificationDto[];
  onDismiss: (id: string) => void;
  onClose: () => void;
}) {
  if (notifications.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-8 text-text-muted">
        <Bell size={24} className="mb-2 opacity-40" />
        <span className="text-sm">No notifications</span>
      </div>
    );
  }

  return (
    <div className="divide-y divide-border/50">
      {notifications.map((n) => {
        const Icon = TYPE_ICONS[n.type] ?? Bell;
        const borderClass = PRIORITY_STYLES[n.priority] ?? PRIORITY_STYLES.Low;

        return (
          <div
            key={n.id}
            className={`flex items-start gap-2.5 px-3 py-2.5 border-l-2 ${borderClass} hover:bg-bg-elevated/30 transition-colors`}
          >
            <Icon size={16} className="text-text-muted shrink-0 mt-0.5" />
            <div className="flex-1 min-w-0">
              <p className="text-sm text-text-primary font-medium truncate">{n.title}</p>
              <p className="text-xs text-text-muted line-clamp-2">{n.message}</p>
              <div className="flex items-center gap-2 mt-1">
                <span className="text-[10px] text-text-muted">{timeAgo(n.createdAt)}</span>
                {n.actionUrl && (
                  <Link
                    href={n.actionUrl}
                    onClick={onClose}
                    className="text-[10px] text-gold hover:text-gold-light transition-colors"
                  >
                    View
                  </Link>
                )}
              </div>
            </div>
            <button
              onClick={() => onDismiss(n.id)}
              className="shrink-0 p-1 text-text-muted hover:text-text-primary transition-colors"
              aria-label="Dismiss notification"
            >
              <X size={14} />
            </button>
          </div>
        );
      })}
    </div>
  );
}
