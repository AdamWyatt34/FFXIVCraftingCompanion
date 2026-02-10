"use client";

import { Search } from "lucide-react";
import Link from "next/link";
import Image from "next/image";
import { useRouter } from "next/navigation";
import { APP_NAME } from "@/lib/constants";
import { useAuth } from "@/contexts/AuthContext";

export function Header() {
  const router = useRouter();
  const { user, isAuthenticated, isLoading } = useAuth();

  return (
    <header className="sticky top-0 z-40 h-14 flex items-center justify-between px-4 bg-bg-secondary border-b border-border">
      <Link href="/" className="text-lg font-bold text-gold tracking-wide">
        {APP_NAME}
      </Link>
      <div className="flex items-center gap-2">
        <button
          onClick={() => router.push("/recipes")}
          className="p-2 rounded-lg text-text-secondary hover:text-gold hover:bg-bg-elevated transition-colors"
          aria-label="Search"
        >
          <Search size={20} />
        </button>

        {!isLoading && (
          isAuthenticated ? (
            <Link
              href="/profile"
              className="shrink-0"
              aria-label="Profile"
            >
              {user?.avatarUrl ? (
                <Image
                  src={user.avatarUrl}
                  alt=""
                  width={28}
                  height={28}
                  className="w-7 h-7 rounded-full border border-gold/40"
                />
              ) : (
                <div className="w-7 h-7 rounded-full bg-gold/20 flex items-center justify-center">
                  <span className="text-xs font-bold text-gold">
                    {(user?.displayName?.[0] ?? user?.email[0] ?? "?").toUpperCase()}
                  </span>
                </div>
              )}
            </Link>
          ) : (
            <Link
              href="/auth/login"
              className="text-sm text-gold hover:text-gold-light transition-colors"
            >
              Sign In
            </Link>
          )
        )}
      </div>
    </header>
  );
}
