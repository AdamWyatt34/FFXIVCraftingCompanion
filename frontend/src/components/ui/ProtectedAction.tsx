"use client";

import { useState, useRef, useEffect } from "react";
import Link from "next/link";
import { LogIn } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";

interface ProtectedActionProps {
  children: React.ReactNode;
  fallback?: React.ReactNode;
  message?: string;
}

export function ProtectedAction({
  children,
  fallback,
  message = "Sign in to use this feature",
}: ProtectedActionProps) {
  const { isAuthenticated } = useAuth();
  const [showTooltip, setShowTooltip] = useState(false);
  const tooltipRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!showTooltip) return;
    const timer = setTimeout(() => setShowTooltip(false), 3000);
    return () => clearTimeout(timer);
  }, [showTooltip]);

  useEffect(() => {
    if (!showTooltip) return;
    function handleClickOutside(e: MouseEvent) {
      if (tooltipRef.current && !tooltipRef.current.contains(e.target as Node)) {
        setShowTooltip(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [showTooltip]);

  if (isAuthenticated) {
    return <>{children}</>;
  }

  if (fallback) {
    return <>{fallback}</>;
  }

  return (
    <div className="relative inline-block" ref={tooltipRef}>
      <div
        onClick={(e) => {
          e.preventDefault();
          e.stopPropagation();
          setShowTooltip(true);
        }}
        className="cursor-pointer"
      >
        {children}
      </div>
      {showTooltip && (
        <div className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 z-50 animate-slide-up">
          <div className="bg-bg-elevated border border-border rounded-lg shadow-lg px-3 py-2 whitespace-nowrap">
            <p className="text-xs text-text-secondary mb-1.5">{message}</p>
            <Link
              href="/auth/login"
              className="inline-flex items-center gap-1.5 text-xs font-medium text-gold hover:text-gold/80 transition-colors"
            >
              <LogIn size={12} />
              Sign in
            </Link>
          </div>
          <div className="absolute top-full left-1/2 -translate-x-1/2 w-2 h-2 bg-bg-elevated border-r border-b border-border rotate-45 -mt-1" />
        </div>
      )}
    </div>
  );
}
