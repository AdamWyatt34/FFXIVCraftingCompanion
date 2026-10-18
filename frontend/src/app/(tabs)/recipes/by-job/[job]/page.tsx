import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { listRecipesServer } from "@/lib/static-data/server";
import {
  CRAFT_JOBS,
  LATEST_BRACKET,
  LEVEL_BRACKETS,
  jobSlug,
  parseJobSlug,
  recipesForJob,
  recipesInBracket,
  sortForHub,
} from "@/lib/static-data/hubs";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import { recipesByJobPath } from "@/lib/routes";
import { breadcrumbListJsonLd } from "@/lib/structured-data";
import { SetBreadcrumbs } from "@/components/layout/SetBreadcrumbs";
import { RecipeHubList } from "@/components/recipes/RecipeHubList";

interface Props {
  params: Promise<{ job: string }>;
}

// Eight pages, built at deploy time from the exported recipe list; anything else is a 404.
export const dynamicParams = false;

export function generateStaticParams(): { job: string }[] {
  return CRAFT_JOBS.map((job) => ({ job: jobSlug(job) }));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const craftType = parseJobSlug((await params).job);
  if (!craftType) return {};
  const job = CRAFT_TYPE_INFO[craftType].fullName;
  const title = `${job} Recipes by Level`;
  const description = `Every Final Fantasy XIV ${job} recipe from level 1 to 100 by level range, each with its ingredient tree, shopping list, crafting order and a solver macro for your stats.`;
  return { title, description, alternates: { canonical: recipesByJobPath(craftType) }, openGraph: { title, description } };
}

export default async function JobHubPage({ params }: Props) {
  const craftType = parseJobSlug((await params).job);
  if (!craftType) notFound();
  const info = CRAFT_TYPE_INFO[craftType];
  const recipes = recipesForJob(await listRecipesServer(), craftType);
  const brackets = LEVEL_BRACKETS.map((bracket) => ({ bracket, count: recipesInBracket(recipes, bracket).length }));
  const latest = sortForHub(recipesInBracket(recipes, LATEST_BRACKET));

  return (
    <div className="p-4 space-y-8 max-w-5xl mx-auto">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{
          __html: breadcrumbListJsonLd([
            { name: "Home", path: "/" },
            { name: "Recipes", path: "/recipes" },
            { name: info.fullName, path: recipesByJobPath(craftType) },
          ]),
        }}
      />
      <SetBreadcrumbs items={[{ label: "Recipes", href: "/recipes" }, { label: info.fullName }]} />

      <header>
        <h1 className="text-2xl font-bold text-text-primary">
          <span className={info.colorClass}>{info.fullName}</span> Recipes
        </h1>
        <p className="text-sm text-text-secondary mt-1">
          {recipes.length.toLocaleString("en-US")} Final Fantasy XIV {info.fullName} recipes from level 1 to 100.
          Pick a level range, or{" "}
          <Link href="/recipes" className="text-gold hover:text-gold-light transition-colors">
            search by item name
          </Link>
          .
        </p>
      </header>

      <nav aria-label={`${info.fullName} recipes by level range`}>
        <ul className="grid grid-cols-2 sm:grid-cols-3 gap-3">
          {brackets.map(({ bracket, count }) => (
            <li key={bracket.slug}>
              <Link
                href={recipesByJobPath(craftType, bracket.slug)}
                className="block p-3 rounded-lg bg-bg-surface border border-border hover:border-gold transition-colors"
              >
                <span className="block text-sm font-medium text-text-primary">{bracket.label}</span>
                <span className="block text-xs text-text-muted mt-0.5">
                  {bracket.expansion} · {count.toLocaleString("en-US")} recipe{count === 1 ? "" : "s"}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      </nav>

      <section aria-labelledby="latest-heading" className="space-y-4">
        <h2 id="latest-heading" className="text-lg font-semibold text-text-primary">
          {LATEST_BRACKET.expansion}: {LATEST_BRACKET.label}
        </h2>
        <RecipeHubList recipes={latest} />
      </section>
    </div>
  );
}
