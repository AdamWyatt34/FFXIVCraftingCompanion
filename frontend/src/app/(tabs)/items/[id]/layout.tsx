import type { Metadata } from "next";

// Inherited by every page below: client-rendered pages whose content is not in the HTML (or
// account pages) are kept out of the index rather than indexed as empty shells.
export const metadata: Metadata = {
  robots: { index: false, follow: true },
};

export default function Layout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
