import type { Metadata } from "next";
import RotationLibraryPageClient from "./RotationLibraryPageClient";

// Server wrapper: the page itself is a client component (search params, hooks), and only a
// server file can publish metadata. The canonical lives here, never in a layout.
export const metadata: Metadata = {
  title: "Rotation Library",
  description:
    "Community-shared FFXIV crafting rotations with their stats, quality and macros, ready to load into the simulator.",
  openGraph: {
    title: "Rotation Library",
    description:
      "Community-shared FFXIV crafting rotations with their stats, quality and macros, ready to load into the simulator.",
  },
};

export default function Page() {
  return <RotationLibraryPageClient />;
}
