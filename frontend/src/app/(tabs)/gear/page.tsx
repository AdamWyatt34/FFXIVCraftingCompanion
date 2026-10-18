import type { Metadata } from "next";
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
  return <GearPageClient />;
}
