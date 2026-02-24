"use client";

import { useState, useEffect } from "react";
import { createPortal } from "react-dom";
import { X, Send, CheckCircle } from "lucide-react";
import { submitFeedback, ApiClientError } from "@/lib/api-client";

const CATEGORIES = [
  { value: "BugReport", label: "Bug Report" },
  { value: "FeatureRequest", label: "Feature Request" },
  { value: "General", label: "General" },
] as const;

const MAX_MESSAGE_LENGTH = 5000;

interface FeedbackModalProps {
  open: boolean;
  onClose: () => void;
}

export function FeedbackModal({ open, onClose }: FeedbackModalProps) {
  const [category, setCategory] = useState("General");
  const [message, setMessage] = useState("");
  const [email, setEmail] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  useEffect(() => {
    if (!open) return;
    function handleKey(e: KeyboardEvent) {
      if (e.key === "Escape") onClose();
    }
    document.addEventListener("keydown", handleKey);
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", handleKey);
      document.body.style.overflow = "";
    };
  }, [open, onClose]);

  function reset() {
    setCategory("General");
    setMessage("");
    setEmail("");
    setError(null);
    setSuccess(false);
  }

  function handleClose() {
    reset();
    onClose();
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!message.trim()) {
      setError("Please enter a message.");
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      await submitFeedback({
        category,
        message: message.trim(),
        email: email.trim() || undefined,
        pageUrl: typeof window !== "undefined" ? window.location.pathname : undefined,
      });
      setSuccess(true);
      setTimeout(handleClose, 2000);
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(
          err.status === 429
            ? "You've submitted too much feedback recently. Please try again later."
            : err.body.error || "Failed to submit feedback.",
        );
      } else {
        setError("An unexpected error occurred.");
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  if (!open) return null;

  return createPortal(
    <div className="fixed inset-0 z-[90] flex items-center justify-center p-4">
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/60"
        onClick={isSubmitting ? undefined : handleClose}
      />

      {/* Dialog */}
      <div className="relative bg-bg-surface border border-border rounded-xl shadow-2xl max-w-md w-full animate-slide-up">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-border">
          <h3 className="text-lg font-semibold text-text-primary">
            Send Feedback
          </h3>
          <button
            onClick={handleClose}
            disabled={isSubmitting}
            className="text-text-muted hover:text-text-secondary transition-colors disabled:opacity-50"
          >
            <X size={18} />
          </button>
        </div>

        {success ? (
          <div className="px-5 py-8 flex flex-col items-center gap-3">
            <CheckCircle size={40} className="text-green-400" />
            <p className="text-sm text-text-secondary">
              Thanks for your feedback!
            </p>
          </div>
        ) : (
          <form onSubmit={handleSubmit}>
            {/* Body */}
            <div className="px-5 py-4 space-y-4">
              {/* Category */}
              <fieldset>
                <legend className="text-sm font-medium text-text-secondary mb-2">
                  Category
                </legend>
                <div className="flex gap-2 flex-wrap">
                  {CATEGORIES.map((cat) => (
                    <button
                      key={cat.value}
                      type="button"
                      onClick={() => setCategory(cat.value)}
                      className={`px-3 py-1.5 rounded-full text-xs font-medium border transition-colors ${
                        category === cat.value
                          ? "border-gold bg-gold/20 text-gold"
                          : "border-border bg-bg-elevated text-text-muted hover:border-border-focus"
                      }`}
                    >
                      {cat.label}
                    </button>
                  ))}
                </div>
              </fieldset>

              {/* Message */}
              <div>
                <label
                  htmlFor="feedback-message"
                  className="text-sm font-medium text-text-secondary mb-1 block"
                >
                  Message
                </label>
                <textarea
                  id="feedback-message"
                  value={message}
                  onChange={(e) =>
                    setMessage(e.target.value.slice(0, MAX_MESSAGE_LENGTH))
                  }
                  rows={4}
                  placeholder="Tell us what's on your mind..."
                  className="w-full rounded-lg bg-bg-elevated border border-border px-3 py-2 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-gold resize-none"
                />
                <div className="text-xs text-text-muted text-right mt-1">
                  {message.length}/{MAX_MESSAGE_LENGTH}
                </div>
              </div>

              {/* Email (optional) */}
              <div>
                <label
                  htmlFor="feedback-email"
                  className="text-sm font-medium text-text-secondary mb-1 block"
                >
                  Email{" "}
                  <span className="text-text-muted font-normal">
                    (optional, for follow-up)
                  </span>
                </label>
                <input
                  id="feedback-email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="you@example.com"
                  className="w-full rounded-lg bg-bg-elevated border border-border px-3 py-2 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-gold"
                />
              </div>

              {error && (
                <div className="p-2 rounded bg-red-500/10 border border-red-500/30 text-red-400 text-xs">
                  {error}
                </div>
              )}
            </div>

            {/* Footer */}
            <div className="flex items-center justify-end gap-3 px-5 py-4 border-t border-border">
              <button
                type="button"
                onClick={handleClose}
                disabled={isSubmitting}
                className="px-4 py-2 text-sm font-medium text-text-secondary hover:text-text-primary transition-colors disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={isSubmitting || !message.trim()}
                className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium rounded-lg bg-gold text-bg-primary hover:bg-gold/80 transition-colors disabled:opacity-50"
              >
                <Send size={14} />
                {isSubmitting ? "Sending..." : "Send"}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>,
    document.body,
  );
}
