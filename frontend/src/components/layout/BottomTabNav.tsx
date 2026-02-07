"use client";

import { usePathname } from "next/navigation";
import Link from "next/link";
import { ChefHat, Shield, FlaskConical, User } from "lucide-react";

const tabs = [
  { href: "/recipes", label: "Recipes", icon: ChefHat },
  { href: "/gear", label: "Gear", icon: Shield },
  { href: "/simulator", label: "Simulator", icon: FlaskConical },
  { href: "/profile", label: "Profile", icon: User },
] as const;

export function BottomTabNav() {
  const pathname = usePathname();

  return (
    <nav className="fixed bottom-0 left-0 right-0 z-50 h-16 bg-bg-secondary border-t border-border flex items-center justify-around">
      {tabs.map((tab) => {
        const isActive = pathname.startsWith(tab.href);
        const Icon = tab.icon;

        return (
          <Link
            key={tab.href}
            href={tab.href}
            className={`flex flex-col items-center justify-center gap-0.5 w-full h-full relative transition-colors ${
              isActive ? "text-tab-active" : "text-tab-inactive"
            }`}
          >
            {isActive && (
              <span className="absolute top-0 left-1/4 right-1/4 h-0.5 bg-gold rounded-full" />
            )}
            <Icon size={22} strokeWidth={isActive ? 2.5 : 1.5} />
            <span className="text-[10px] font-medium">{tab.label}</span>
          </Link>
        );
      })}
    </nav>
  );
}
