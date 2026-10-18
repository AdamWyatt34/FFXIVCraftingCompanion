"use client";

import { useState, useEffect, useCallback } from "react";

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: "accepted" | "dismissed" }>;
}

const VISIT_KEY = "ffxiv_visit_count";
const DISMISS_KEY = "ffxiv_install_dismissed";
const VISIT_THRESHOLD = 3;
/** A dismissal hides the banner for a month, not forever. */
const DISMISS_TTL_MS = 30 * 24 * 60 * 60 * 1000;

function readDismissedAt(): number | null {
  try {
    const raw = localStorage.getItem(DISMISS_KEY);
    if (!raw) return null;
    // Older builds stored "1"; treat it as dismissed now so the month starts from this visit.
    const at = raw === "1" ? Date.now() : Number(raw);
    if (!Number.isFinite(at)) return null;
    if (Date.now() - at > DISMISS_TTL_MS) {
      localStorage.removeItem(DISMISS_KEY);
      return null;
    }
    if (raw === "1") localStorage.setItem(DISMISS_KEY, String(at));
    return at;
  } catch {
    return null;
  }
}

function rememberDismissal(): void {
  try {
    localStorage.setItem(DISMISS_KEY, String(Date.now()));
  } catch {
    // ignore quota errors
  }
}

export function useInstallPrompt() {
  const [deferredPrompt, setDeferredPrompt] =
    useState<BeforeInstallPromptEvent | null>(null);
  const [showBanner, setShowBanner] = useState(false);

  useEffect(() => {
    if (typeof window === "undefined") return;

    // The browser fires beforeinstallprompt once, early; the listener is registered before
    // anything else so the event is never missed, and whether to show the banner is decided
    // when it arrives.
    let visits = 0;
    try {
      visits = Number(localStorage.getItem(VISIT_KEY) || "0") + 1;
      localStorage.setItem(VISIT_KEY, String(visits));
    } catch {
      // ignore storage errors; the banner simply stays hidden
    }
    const eligible = readDismissedAt() === null && visits >= VISIT_THRESHOLD;

    function handleBeforeInstall(e: Event) {
      e.preventDefault();
      setDeferredPrompt(e as BeforeInstallPromptEvent);
      if (eligible) setShowBanner(true);
    }

    function handleInstalled() {
      setShowBanner(false);
      setDeferredPrompt(null);
    }

    window.addEventListener("beforeinstallprompt", handleBeforeInstall);
    window.addEventListener("appinstalled", handleInstalled);
    return () => {
      window.removeEventListener("beforeinstallprompt", handleBeforeInstall);
      window.removeEventListener("appinstalled", handleInstalled);
    };
  }, []);

  const install = useCallback(async () => {
    if (!deferredPrompt) return;
    await deferredPrompt.prompt();
    const { outcome } = await deferredPrompt.userChoice;
    if (outcome === "dismissed") rememberDismissal();
    setShowBanner(false);
    setDeferredPrompt(null);
  }, [deferredPrompt]);

  const dismiss = useCallback(() => {
    setShowBanner(false);
    setDeferredPrompt(null);
    rememberDismissal();
  }, []);

  return { showBanner, install, dismiss };
}
