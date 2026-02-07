"use client";

import { Search } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { APP_NAME } from "@/lib/constants";

export function Header() {
  const router = useRouter();

  return (
    <header className="sticky top-0 z-40 h-14 flex items-center justify-between px-4 bg-bg-secondary border-b border-border">
      <Link href="/" className="text-lg font-bold text-gold tracking-wide">
        {APP_NAME}
      </Link>
      <button
        onClick={() => router.push("/recipes")}
        className="p-2 rounded-lg text-text-secondary hover:text-gold hover:bg-bg-elevated transition-colors"
        aria-label="Search"
      >
        <Search size={20} />
      </button>
    </header>
  );
}
