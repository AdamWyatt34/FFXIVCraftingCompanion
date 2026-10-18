"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { UserPlus, Eye, EyeOff, Check, X } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { ApiClientError } from "@/lib/api-client";
import { LoadingSpinner } from "@/components/ui";

function PasswordRequirement({ met, label }: { met: boolean; label: string }) {
  return (
    <div className={`flex items-center gap-1.5 text-xs ${met ? "text-emerald-400" : "text-text-muted"}`}>
      {met ? <Check size={12} /> : <X size={12} />}
      {label}
    </div>
  );
}

export default function RegisterPage() {
  const router = useRouter();
  const { register, loginWithDiscord } = useAuth();

  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isDiscordLoading, setIsDiscordLoading] = useState(false);

  const hasMinLength = password.length >= 8;
  const hasUppercase = /[A-Z]/.test(password);
  const hasNumber = /\d/.test(password);
  const passwordsMatch = password === confirmPassword && confirmPassword.length > 0;
  const isFormValid = email && hasMinLength && hasUppercase && hasNumber && passwordsMatch;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!isFormValid) return;

    setError(null);
    setIsSubmitting(true);

    try {
      await register({
        email,
        password,
        displayName: displayName || undefined,
      });
      router.push("/profile");
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(
          err.status === 409
            ? "An account with this email already exists."
            : err.body.error || "Registration failed. Please try again.",
        );
      } else {
        setError("An unexpected error occurred. Please try again.");
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleDiscord() {
    setIsDiscordLoading(true);
    try {
      await loginWithDiscord();
    } catch {
      setError("Failed to start Discord sign-up.");
      setIsDiscordLoading(false);
    }
  }

  return (
    <div className="w-full max-w-md space-y-6">
      <div className="text-center">
        <UserPlus className="mx-auto text-gold mb-3" size={32} />
        <h1 className="text-2xl font-bold text-gold">Create Account</h1>
        <p className="text-text-secondary text-sm mt-1">
          Join FFXIV Crafting Companion
        </p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label htmlFor="email" className="block text-sm font-medium text-text-secondary mb-1">
            Email
          </label>
          <input
            id="email"
            type="email"
            required
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="w-full h-11 px-3 bg-bg-secondary border border-border rounded-lg text-text-primary placeholder:text-text-muted focus:outline-none focus:border-border-focus transition-colors"
            placeholder="you@example.com"
          />
        </div>

        <div>
          <label htmlFor="displayName" className="block text-sm font-medium text-text-secondary mb-1">
            Display Name <span className="text-text-muted">(optional)</span>
          </label>
          <input
            id="displayName"
            type="text"
            autoComplete="name"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            className="w-full h-11 px-3 bg-bg-secondary border border-border rounded-lg text-text-primary placeholder:text-text-muted focus:outline-none focus:border-border-focus transition-colors"
            placeholder="Your display name"
          />
        </div>

        <div>
          <label htmlFor="password" className="block text-sm font-medium text-text-secondary mb-1">
            Password
          </label>
          <div className="relative">
            <input
              id="password"
              type={showPassword ? "text" : "password"}
              required
              autoComplete="new-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full h-11 px-3 pr-10 bg-bg-secondary border border-border rounded-lg text-text-primary placeholder:text-text-muted focus:outline-none focus:border-border-focus transition-colors"
              placeholder="Create a password"
            />
            <button
              type="button"
              onClick={() => setShowPassword(!showPassword)}
              className="absolute right-3 top-1/2 -translate-y-1/2 text-text-muted hover:text-text-secondary transition-colors"
              aria-label={showPassword ? "Hide password" : "Show password"}
            >
              {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
            </button>
          </div>
          {password.length > 0 && (
            <div className="flex flex-wrap gap-x-4 gap-y-1 mt-2">
              <PasswordRequirement met={hasMinLength} label="8+ characters" />
              <PasswordRequirement met={hasUppercase} label="1 uppercase" />
              <PasswordRequirement met={hasNumber} label="1 number" />
            </div>
          )}
        </div>

        <div>
          <label htmlFor="confirmPassword" className="block text-sm font-medium text-text-secondary mb-1">
            Confirm Password
          </label>
          <input
            id="confirmPassword"
            type={showPassword ? "text" : "password"}
            required
            autoComplete="new-password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            className={`w-full h-11 px-3 bg-bg-secondary border rounded-lg text-text-primary placeholder:text-text-muted focus:outline-none transition-colors ${
              confirmPassword.length > 0 && !passwordsMatch
                ? "border-red-500/50 focus:border-red-500"
                : "border-border focus:border-border-focus"
            }`}
            placeholder="Confirm your password"
          />
          {confirmPassword.length > 0 && !passwordsMatch && (
            <p className="text-xs text-red-400 mt-1">Passwords do not match</p>
          )}
        </div>

        {error && (
          <div className="p-3 rounded-lg bg-red-500/10 border border-red-500/30 text-red-400 text-sm">
            {error}
          </div>
        )}

        <button
          type="submit"
          disabled={isSubmitting || !isFormValid}
          className="w-full h-11 bg-gold text-bg-primary font-semibold rounded-lg hover:bg-gold-light disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center justify-center gap-2"
        >
          {isSubmitting ? <LoadingSpinner size={18} /> : "Create Account"}
        </button>
      </form>

      <div className="relative">
        <div className="absolute inset-0 flex items-center">
          <span className="w-full border-t border-border" />
        </div>
        <div className="relative flex justify-center text-xs">
          <span className="bg-bg-primary px-2 text-text-muted">or</span>
        </div>
      </div>

      <button
        onClick={handleDiscord}
        disabled={isDiscordLoading}
        className="w-full h-11 bg-[#5865F2] text-white font-semibold rounded-lg hover:bg-[#4752C4] disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center justify-center gap-2"
      >
        {isDiscordLoading ? (
          <LoadingSpinner size={18} />
        ) : (
          <>
            <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
              <path d="M20.317 4.37a19.791 19.791 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057a.082.082 0 0 0 .031.057 19.9 19.9 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028 14.09 14.09 0 0 0 1.226-1.994.076.076 0 0 0-.041-.106 13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128 10.2 10.2 0 0 0 .372-.292.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127 12.299 12.299 0 0 1-1.873.892.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.839 19.839 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.095 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.095 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z" />
            </svg>
            Sign up with Discord
          </>
        )}
      </button>

      <p className="text-center text-sm text-text-secondary">
        Already have an account?{" "}
        <Link href="/auth/login" className="text-gold underline underline-offset-2 hover:text-gold-light transition-colors">
          Sign in
        </Link>
      </p>
    </div>
  );
}
