import Link from "next/link";

export default function NotFound() {
  return (
    <div className="flex flex-col items-center justify-center min-h-[60vh] p-4 text-center">
      <h1 className="text-6xl font-bold text-gold mb-4">404</h1>
      <p className="text-xl text-text-secondary mb-6">
        This page has been lost to the Lifestream.
      </p>
      <Link
        href="/recipes"
        className="px-6 py-3 bg-gold text-bg-primary font-semibold rounded-lg hover:bg-gold-light transition-colors"
      >
        Return to Recipes
      </Link>
    </div>
  );
}
