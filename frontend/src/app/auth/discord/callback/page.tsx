"use client";

import { Suspense, useEffect, useState } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { LoadingSpinner } from "@/components/ui";

function DiscordCallbackContent() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const { handleDiscordCallback } = useAuth();

  const [error, setError] = useState<string | null>(null);
  const [hasAttempted, setHasAttempted] = useState(false);

  useEffect(() => {
    if (hasAttempted) return;
    setHasAttempted(true);

    const code = searchParams.get("code");
    if (!code) {
      setError("No authorization code received from Discord.");
      return;
    }

    (async () => {
      try {
        await handleDiscordCallback(code);
        router.push("/profile");
      } catch {
        setError("Failed to sign in with Discord. Please try again.");
      }
    })();
  }, [searchParams, handleDiscordCallback, router, hasAttempted]);

  if (error) {
    return (
      <div className="min-h-screen flex items-center justify-center p-4">
        <div className="text-center space-y-4 max-w-sm">
          <div className="p-3 rounded-lg bg-red-500/10 border border-red-500/30 text-red-400 text-sm">
            {error}
          </div>
          <button
            onClick={() => router.push("/auth/login")}
            className="px-4 h-10 bg-bg-elevated text-text-primary rounded-lg hover:bg-bg-surface transition-colors text-sm"
          >
            Back to Sign In
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex items-center justify-center p-4">
      <div className="text-center space-y-3">
        <LoadingSpinner size={32} />
        <p className="text-text-secondary">Signing in with Discord...</p>
      </div>
    </div>
  );
}

export default function DiscordCallbackPage() {
  return (
    <Suspense
      fallback={
        <div className="min-h-screen flex items-center justify-center p-4">
          <div className="text-center space-y-3">
            <LoadingSpinner size={32} />
            <p className="text-text-secondary">Loading...</p>
          </div>
        </div>
      }
    >
      <DiscordCallbackContent />
    </Suspense>
  );
}
