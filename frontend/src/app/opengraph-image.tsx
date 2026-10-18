import { ImageResponse } from "next/og";

// Text-only card, generated at build time; no remote fetch, so it costs nothing per share.
export const alt = "FFXIV Crafting Companion: recipes, simulator, solver and gear for every crafter";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

export default function OpenGraphImage() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          padding: "72px 88px",
          background: "linear-gradient(135deg, #1a1a2e 0%, #16213e 60%, #0f3460 100%)",
          color: "#e0e0e0",
          fontFamily: "sans-serif",
        }}
      >
        <div style={{ display: "flex", fontSize: 30, letterSpacing: 6, color: "#e6b422" }}>
          FINAL FANTASY XIV
        </div>
        <div style={{ display: "flex", fontSize: 84, fontWeight: 700, marginTop: 18, color: "#ffffff" }}>
          Crafting Companion
        </div>
        <div style={{ display: "flex", fontSize: 34, marginTop: 28, color: "#c8c8d8", lineHeight: 1.35 }}>
          Every recipe, a step-by-step simulator, a rotation solver and best-in-slot gear for all eight
          crafters.
        </div>
        <div style={{ display: "flex", marginTop: 56, gap: 20 }}>
          {["Recipes", "Simulator", "Solver", "Gear"].map((label) => (
            <div
              key={label}
              style={{
                display: "flex",
                padding: "12px 26px",
                borderRadius: 999,
                border: "2px solid #e6b422",
                color: "#e6b422",
                fontSize: 28,
              }}
            >
              {label}
            </div>
          ))}
        </div>
      </div>
    ),
    size,
  );
}
