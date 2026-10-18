import type { Metadata } from "next";
import CraftPlanPageClient from "./CraftPlanPageClient";

// Server wrapper: the page itself is a client component (search params, hooks), and only a
// server file can publish metadata. The canonical lives here, never in a layout.
export const metadata: Metadata = {
  title: "Craft Plan: Materials for Several Recipes",
  description:
    "Plan several FFXIV crafts at once. Combined raw materials, crystals, intermediate crafts and a shopping list, resolved in your browser.",
  alternates: { canonical: "/recipes/plan" },
  openGraph: {
    title: "Craft Plan: Materials for Several Recipes",
    description:
      "Plan several FFXIV crafts at once. Combined raw materials, crystals, intermediate crafts and a shopping list, resolved in your browser.",
  },
};

export default function Page() {
  return <CraftPlanPageClient />;
}
