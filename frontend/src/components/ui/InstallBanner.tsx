"use client";

import { useInstallPrompt } from "@/hooks/useInstallPrompt";

export function InstallBanner() {
  const { showBanner, install, dismiss } = useInstallPrompt();

  if (!showBanner) return null;

  return (
    <div className="fixed bottom-16 left-0 right-0 z-40 px-4 pb-2 animate-slide-up">
      <div className="max-w-lg mx-auto flex items-center gap-3 rounded-lg border border-gold/40 bg-bg-surface p-3 shadow-lg">
        <div className="flex-1 min-w-0">
          <p className="text-sm font-medium text-gold">Install FFXIV Craft</p>
          <p className="text-xs text-text-secondary truncate">
            Quick access from your home screen
          </p>
        </div>
        <button
          onClick={install}
          className="shrink-0 px-3 py-1.5 rounded-md bg-gold text-bg-primary text-sm font-medium hover:bg-gold-light transition-colors"
        >
          Install
        </button>
        <button
          onClick={dismiss}
          className="shrink-0 px-2 py-1.5 rounded-md text-sm text-text-muted hover:text-text-secondary transition-colors"
        >
          Dismiss
        </button>
      </div>
    </div>
  );
}
