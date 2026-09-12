import type { Metadata, Viewport } from "next";
import { Header, BottomTabNav, Footer } from "@/components/layout";
import { ToastProvider, Breadcrumbs, InstallBanner } from "@/components/ui";
import { FeedbackButton } from "@/components/feedback";
import { EngineModeFromUrl } from "@/components/EngineModeFromUrl";
import { AuthProvider } from "@/contexts/AuthContext";
import { CraftPlanProvider } from "@/contexts/CraftPlanContext";
import { BreadcrumbProvider } from "@/contexts/BreadcrumbContext";
import "./globals.css";

export const metadata: Metadata = {
  metadataBase: new URL(
    process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000",
  ),
  title: {
    default: "FFXIV Crafting Companion — Recipe Explorer, Simulator & Solver",
    template: "%s | FFXIV Crafting Companion",
  },
  description:
    "Free crafting companion for Final Fantasy XIV. Browse recipes, simulate rotations, auto-solve macros, plan shopping lists, and find best-in-slot gear for every crafter and gatherer.",
  keywords: [
    "FFXIV",
    "Final Fantasy XIV",
    "crafting",
    "simulator",
    "rotation solver",
    "macro",
    "recipe",
    "gear",
    "Dawntrail",
    "shopping list",
    "material tree",
  ],
  openGraph: {
    type: "website",
    siteName: "FFXIV Crafting Companion",
    title: "FFXIV Crafting Companion — Recipe Explorer, Simulator & Solver",
    description:
      "Free crafting companion for Final Fantasy XIV. Browse recipes, simulate rotations, auto-solve macros, and find best-in-slot gear.",
  },
  twitter: {
    card: "summary_large_image",
    title: "FFXIV Crafting Companion",
    description:
      "Browse recipes, simulate rotations, auto-solve macros, and find best-in-slot gear for FFXIV.",
  },
  icons: {
    icon: [
      { url: "/icons/favicon-196.png", sizes: "196x196", type: "image/png" },
    ],
    apple: [
      { url: "/icons/apple-icon-180.png", sizes: "180x180", type: "image/png" },
    ],
  },
  robots: { index: true, follow: true },
  manifest: "/manifest.json",
  appleWebApp: {
    capable: true,
    statusBarStyle: "black-translucent",
    title: "FFXIV Craft",
  },
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  maximumScale: 1,
  themeColor: "#e6b422",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className="min-h-screen bg-bg-primary text-text-primary antialiased">
        <EngineModeFromUrl />
        <AuthProvider>
          <BreadcrumbProvider>
            <Header />
            <Breadcrumbs />
            <CraftPlanProvider>
              <ToastProvider>
                <main className="flex-1 pb-16">{children}</main>
              </ToastProvider>
            </CraftPlanProvider>
            <Footer />
            <FeedbackButton />
            <InstallBanner />
            <BottomTabNav />
          </BreadcrumbProvider>
        </AuthProvider>
      </body>
    </html>
  );
}
