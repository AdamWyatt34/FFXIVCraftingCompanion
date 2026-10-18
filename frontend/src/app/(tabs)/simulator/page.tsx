import type { Metadata } from "next";
import SimulatorPageClient from "./SimulatorPageClient";

// Server wrapper: the page itself is a client component (search params, hooks), and only a
// server file can publish metadata. The canonical lives here, never in a layout.
export const metadata: Metadata = {
  title: "Crafting Simulator & Rotation Solver",
  description:
    "Simulate FFXIV crafting rotations step by step and let the solver find a macro for your stats. Runs in your browser and works offline.",
  alternates: { canonical: "/simulator" },
  openGraph: {
    title: "Crafting Simulator & Rotation Solver",
    description:
      "Simulate FFXIV crafting rotations step by step and let the solver find a macro for your stats. Runs in your browser and works offline.",
  },
};

export default function Page() {
  return <SimulatorPageClient />;
}
