import type { Metadata } from "next";
import Form from "next/form";
import Link from "next/link";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import { CRAFT_JOBS, LATEST_BRACKET } from "@/lib/static-data/hubs";
import { DAWNTRAIL_RECIPES_PATH, recipesByJobPath } from "@/lib/routes";
import {
  ChefHat,
  FlaskConical,
  ShoppingCart,
  Shield,
  BarChart3,
  Search,
  Hammer,
  ListChecks,
  ArrowRight,
  Package,
} from "lucide-react";

export const metadata: Metadata = {
  alternates: { canonical: "/" },
};

const FEATURES = [
  {
    Icon: ChefHat,
    title: "Recipe Explorer",
    href: "/recipes",
    description:
      "Browse thousands of crafting recipes. View ingredient trees, shopping lists, and step-by-step crafting order.",
  },
  {
    Icon: FlaskConical,
    title: "Crafting Simulator & Rotation Solver",
    href: "/simulator",
    description:
      "Test rotations without wasting materials, or let the greedy and MCTS solvers find one for your stats. Simulates every Dawntrail crafting action.",
  },
  {
    Icon: ShoppingCart,
    title: "Craft Planner",
    href: "/recipes/plan",
    description:
      "Plan multi-item crafting sessions. Aggregate shopping lists and track your progress.",
  },
  {
    Icon: Shield,
    title: "Gear Browser",
    href: "/gear",
    description:
      "Find best-in-slot gear for any crafter or gatherer class with stat-weight recommendations.",
  },
  {
    Icon: BarChart3,
    title: "Market Prices",
    href: "/recipes",
    description:
      "Check Universalis market board prices. See cost breakdowns and profit margins for crafts.",
  },
] as const;

const STEPS = [
  { Icon: Search, text: "Search for a recipe or item" },
  { Icon: Package, text: "View the full material tree" },
  { Icon: Shield, text: "Check gear recommendations" },
  { Icon: Hammer, text: "Simulate or auto-solve a rotation" },
  { Icon: ListChecks, text: "Export your macro and craft" },
] as const;

export default function HomePage() {
  return (
    <div className="max-w-5xl mx-auto px-4 py-12 space-y-20">
      {/* Hero */}
      <section className="text-center space-y-6">
        <h1 className="text-4xl sm:text-5xl font-bold text-text-primary">
          FFXIV Crafting{" "}
          <span className="text-gold">Companion</span>
        </h1>
        <p className="text-lg text-text-secondary max-w-2xl mx-auto">
          One-stop crafting, gear &amp; materials platform for Final Fantasy XIV.
          Explore recipes, simulate rotations, and plan your crafting sessions.
        </p>
        <Form action="/recipes" role="search" className="flex gap-2 max-w-xl mx-auto">
          <label htmlFor="home-search" className="sr-only">
            Search recipes and items
          </label>
          <div className="relative flex-1">
            <Search
              size={18}
              aria-hidden="true"
              className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted pointer-events-none"
            />
            <input
              id="home-search"
              name="q"
              type="search"
              autoComplete="off"
              placeholder="Search a recipe or item"
              className="w-full h-12 pl-10 pr-3 bg-bg-secondary text-text-primary placeholder:text-text-muted rounded-lg border border-border focus:border-border-focus focus:outline-none transition-colors"
            />
          </div>
          <button
            type="submit"
            className="h-12 px-5 rounded-lg bg-gold text-bg-primary font-semibold hover:bg-gold-light transition-colors"
          >
            Search
          </button>
        </Form>
        <div className="flex justify-center gap-4 flex-wrap">
          <Link
            href="/recipes"
            className="inline-flex items-center gap-2 px-6 py-3 rounded-lg bg-gold text-bg-primary font-semibold hover:bg-gold-light transition-colors"
          >
            Start Crafting
            <ArrowRight size={18} />
          </Link>
          <Link
            href="/simulator"
            className="inline-flex items-center gap-2 px-6 py-3 rounded-lg border border-border text-text-primary font-medium hover:border-gold hover:text-gold transition-colors"
          >
            Open Simulator
          </Link>
        </div>
      </section>

      {/* Features */}
      <section className="space-y-8">
        <h2 className="text-2xl font-bold text-text-primary text-center">
          Everything You Need
        </h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {FEATURES.map(({ Icon, title, href, description }) => (
            <Link
              key={title}
              href={href}
              className="bg-bg-surface border border-border rounded-lg p-5 space-y-3 hover:border-gold/40 transition-colors group"
            >
              <Icon size={28} className="text-gold" />
              <h3 className="text-lg font-semibold text-text-primary group-hover:text-gold transition-colors">
                {title}
              </h3>
              <p className="text-sm text-text-secondary leading-relaxed">
                {description}
              </p>
            </Link>
          ))}
        </div>
      </section>

      {/* Browse by job: the crawlable way into the recipe hubs */}
      <section className="space-y-6" aria-labelledby="browse-heading">
        <h2 id="browse-heading" className="text-2xl font-bold text-text-primary text-center">
          Browse Recipes by Job
        </h2>
        <ul className="flex flex-wrap justify-center gap-3">
          {CRAFT_JOBS.map((job) => (
            <li key={job}>
              <Link
                href={recipesByJobPath(job)}
                className={`inline-block px-4 py-2 rounded-full bg-bg-surface border border-border hover:border-gold transition-colors text-sm font-medium ${CRAFT_TYPE_INFO[job].colorClass}`}
              >
                {CRAFT_TYPE_INFO[job].fullName}
              </Link>
            </li>
          ))}
          <li>
            <Link
              href={DAWNTRAIL_RECIPES_PATH}
              className="inline-block px-4 py-2 rounded-full bg-gold/10 border border-gold/40 hover:border-gold transition-colors text-sm font-medium text-gold"
            >
              {LATEST_BRACKET.expansion} Recipes ({LATEST_BRACKET.label})
            </Link>
          </li>
        </ul>
      </section>

      {/* How It Works */}
      <section className="space-y-8">
        <h2 className="text-2xl font-bold text-text-primary text-center">
          How It Works
        </h2>
        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-center gap-4 sm:gap-2">
          {STEPS.map(({ Icon, text }, i) => (
            <div key={text} className="flex items-center gap-2">
              <div className="flex items-center gap-3 bg-bg-surface border border-border rounded-lg px-4 py-3">
                <span className="flex items-center justify-center w-7 h-7 rounded-full bg-gold/20 text-gold text-sm font-bold shrink-0">
                  {i + 1}
                </span>
                <Icon size={18} className="text-text-muted shrink-0" />
                <span className="text-sm text-text-primary">{text}</span>
              </div>
              {i < STEPS.length - 1 && (
                <ArrowRight
                  size={16}
                  className="text-text-muted hidden sm:block shrink-0"
                />
              )}
            </div>
          ))}
        </div>
      </section>

      {/* Open Source */}
      <section className="text-center space-y-3 bg-bg-surface border border-border rounded-lg p-8">
        <h2 className="text-xl font-bold text-text-primary">
          Built by the Community
        </h2>
        <p className="text-sm text-text-secondary max-w-lg mx-auto">
          FFXIV Crafting Companion is open source and free to use.
          Contributions, feedback, and bug reports are always welcome.
        </p>
        <a
          href="https://github.com/AdamWyatt34/FFXIVCraftingCompanion"
          className="inline-flex items-center gap-2 text-sm font-medium text-gold underline underline-offset-2 hover:text-gold-light transition-colors"
        >
          View the source on GitHub
        </a>
      </section>

    </div>
  );
}
