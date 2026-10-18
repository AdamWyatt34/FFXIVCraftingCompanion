import type { Metadata } from "next";
import { Shield } from "lucide-react";
import GearPageClient from "./GearPageClient";

// Server wrapper: the page itself is a client component (search params, hooks), and only a
// server file can publish metadata. The canonical lives here, never in a layout.
export const metadata: Metadata = {
  title: "Crafter & Gatherer Gear Recommendations",
  description:
    "Best-in-slot gear for every FFXIV crafter and gatherer job at any level, weighted for craftsmanship, control and CP, with the recipes to make it.",
  alternates: { canonical: "/gear" },
  openGraph: {
    title: "Crafter & Gatherer Gear Recommendations",
    description:
      "Best-in-slot gear for every FFXIV crafter and gatherer job at any level, weighted for craftsmanship, control and CP, with the recipes to make it.",
  },
};

export default function Page() {
  return (
    <div className="flex flex-col h-full">
      <div className="p-4 border-b border-border">
        <div className="flex items-center gap-2 mb-1">
          <Shield className="text-gold" size={24} />
          <h1 className="text-xl font-bold">Gear Recommendations</h1>
        </div>
        <p className="text-text-secondary text-sm">
          Best-in-slot crafter and gatherer gear for your job and level, with the recipes to make it
        </p>
      </div>
      <GearPageClient />
    </div>
  );
}
