import type { Metadata, Viewport } from "next";
import { Header, BottomTabNav, Footer } from "@/components/layout";
import { ToastProvider, Breadcrumbs, InstallBanner, ApiStatusBanner } from "@/components/ui";
import { FeedbackButton } from "@/components/feedback";
import Script from "next/script";
import { AuthProvider } from "@/contexts/AuthContext";
import { CraftPlanProvider } from "@/contexts/CraftPlanContext";
import { BreadcrumbProvider } from "@/contexts/BreadcrumbContext";
import { getSiteUrl } from "@/lib/site-url";
import "./globals.css";

// No canonical here on purpose: metadata is inherited by every nested page without its own, so a
// root canonical would point every client-rendered page at "/". Each indexable page sets its own.
export const metadata: Metadata = {
  metadataBase: new URL(getSiteUrl()),
  title: {
    default: "FFXIV Crafting Companion — Recipe Explorer, Simulator & Solver",
    template: "%s | FFXIV Craft",
  },
  description:
    "Free crafting companion for Final Fantasy XIV. Browse recipes, simulate rotations, auto-solve macros, plan shopping lists, and find best-in-slot gear for every crafter and gatherer.",
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
        <a
          href="#main"
          className="sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-[100] focus:rounded-md focus:bg-gold focus:px-3 focus:py-2 focus:text-sm focus:font-medium focus:text-bg-primary"
        >
          Skip to content
        </a>
        <Script id="vercel-analytics-queue" strategy="beforeInteractive">
          {"window.va = window.va || function () { (window.vaq = window.vaq || []).push(arguments); };"}
        </Script>
        <Script src="/_vercel/insights/script.js" strategy="afterInteractive" />
        <AuthProvider>
          <BreadcrumbProvider>
            <Header />
            <ApiStatusBanner />
            <Breadcrumbs />
            <CraftPlanProvider>
              <ToastProvider>
                <main id="main" className="flex-1 pb-16">{children}</main>
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
