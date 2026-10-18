import type { Metadata } from "next";
import RecipesPageClient from "./RecipesPageClient";

// Server wrapper: the page itself is a client component (search params, hooks), and only a
// server file can publish metadata. The canonical lives here, never in a layout.
export const metadata: Metadata = {
  title: "FFXIV Recipe Browser",
  description:
    "Search every Final Fantasy XIV crafting recipe by name, job and level. Ingredient trees, shopping lists and crafting order for all eight Disciples of the Hand.",
  alternates: { canonical: "/recipes" },
  openGraph: {
    title: "FFXIV Recipe Browser",
    description:
      "Search every Final Fantasy XIV crafting recipe by name, job and level. Ingredient trees, shopping lists and crafting order for all eight Disciples of the Hand.",
  },
};

export default function Page() {
  return <RecipesPageClient />;
}
