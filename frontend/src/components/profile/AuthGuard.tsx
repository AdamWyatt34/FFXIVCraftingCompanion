"use client";

import { useAuth } from "@/contexts/AuthContext";
import { ProfileLoadingSkeleton } from "./ProfileLoadingSkeleton";
import { SignInPrompt } from "./SignInPrompt";

interface AuthGuardProps {
  children: React.ReactNode;
  fallback?: React.ReactNode;
}

export function AuthGuard({ children, fallback }: AuthGuardProps) {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) return <ProfileLoadingSkeleton />;
  if (!isAuthenticated) return <>{fallback ?? <SignInPrompt />}</>;
  return <>{children}</>;
}
