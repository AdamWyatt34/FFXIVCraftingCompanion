"use client";

import { Suspense, useEffect, useRef, useState } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import { useAuth, OAuthStateError } from "@/contexts/AuthContext";
import { LoadingSpinner } from "@/components/ui";

function DiscordCallbackContent() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const { handleDiscordCallback } = useAuth();

  const [error, setError] = useState<string | null>(null);
  const hasAttemptedRef = useRef(false);

  useEffect(() => {
    if (hasAttemptedRef.current) return;
    hasAttemptedRef.current = true;

    (async () => {
      if (searchParams.get("error")) {
        setError("Discord sign-in was cancelled.");
        return;
      }

      const code = searchParams.get("code");
      const state = searchParams.get("state");
      if (!code) {
        setError("No authorization code received from Discord.");
        return;
      }

      try {
        await handleDiscordCallback(code, state ?? "");
        router.push("/profile");
      } catch (err) {
        setError(
          err instanceof OAuthStateError
            ? err.message
            : "Failed to sign in with Discord. Please try again.",
        );
      }
    })();
  }, [searchParams, handleDiscordCallback, router]);

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
