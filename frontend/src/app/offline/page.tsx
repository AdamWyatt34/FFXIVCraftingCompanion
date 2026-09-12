import Link from "next/link";

export const metadata = {
  title: "Offline",
};

export default function OfflinePage() {
  return (
    <div className="flex flex-col items-center justify-center min-h-[60vh] p-6 text-center">
      {/* Inline SVG — no JS chunk dependency */}
      <svg
        xmlns="http://www.w3.org/2000/svg"
        width="64"
        height="64"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
        className="text-text-muted mb-6"
        aria-hidden="true"
      >
        <line x1="2" x2="22" y1="2" y2="22" />
        <path d="M8.5 16.5a5 5 0 0 1 7 0" />
        <path d="M2 8.82a15 15 0 0 1 4.17-2.65" />
        <path d="M10.66 5c4.01-.36 8.14.9 11.34 3.76" />
        <path d="M16.85 11.25a10 10 0 0 1 2.22 1.68" />
        <path d="M5 12.859a10 10 0 0 1 5.17-2.69" />
        <circle cx="12" cy="20" r="1" />
      </svg>

      <h1 className="text-2xl font-bold text-text-primary mb-2">
        You&apos;re Offline
      </h1>
      <p className="text-text-secondary mb-8 max-w-md">
        It looks like you&apos;ve lost your internet connection. Some features
        are still available from your cache.
      </p>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 w-full max-w-md mb-8">
        <div className="bg-bg-surface border border-border rounded-lg p-4">
          <h2 className="text-sm font-semibold text-green-400 mb-2">
            Available Offline
          </h2>
          <ul className="text-sm text-text-secondary space-y-1 text-left">
            <li>Previously viewed recipes</li>
            <li>Cached item data</li>
            <li>Gear recommendations</li>
            <li>Crafting simulator</li>
            <li>Rotation solver (cached recipes)</li>
          </ul>
        </div>
        <div className="bg-bg-surface border border-border rounded-lg p-4">
          <h2 className="text-sm font-semibold text-red-400 mb-2">
            Requires Internet
          </h2>
          <ul className="text-sm text-text-secondary space-y-1 text-left">
            <li>Market prices</li>
            <li>New searches</li>
            <li>Account sync</li>
          </ul>
        </div>
      </div>

      <Link
        href="/recipes"
        className="inline-flex items-center gap-2 px-6 py-3 rounded-lg bg-gold text-bg-primary font-medium hover:bg-gold-light transition-colors"
      >
        Try Cached Recipes
      </Link>
    </div>
  );
}
