import type { Metadata, Viewport } from "next";
import { Header, BottomTabNav } from "@/components/layout";
import { ToastProvider } from "@/components/ui";
import "./globals.css";

export const metadata: Metadata = {
  title: "FFXIV Crafting Companion",
  description: "A crafting companion app for Final Fantasy XIV",
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
        <Header />
        <ToastProvider>
          <main className="flex-1 pb-16">{children}</main>
        </ToastProvider>
        <BottomTabNav />
      </body>
    </html>
  );
}
