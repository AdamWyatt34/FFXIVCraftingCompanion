import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { listRecipesServer } from "@/lib/static-data/server";
import {
  LEVEL_BRACKETS,
  findBracket,
  hubParams,
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
  params: Promise<{ job: string; bracket: string }>;
}

// Forty-eight pages (eight jobs, six level ranges), built at deploy time; anything else is a 404.
export const dynamicParams = false;

export function generateStaticParams(): { job: string; bracket: string }[] {
  return hubParams();
}

async function resolve(params: Props["params"]) {
  const { job, bracket: bracketSlug } = await params;
  const craftType = parseJobSlug(job);
  const bracket = findBracket(bracketSlug);
  if (!craftType || !bracket) return null;
  const recipes = sortForHub(recipesInBracket(recipesForJob(await listRecipesServer(), craftType), bracket));
  return { craftType, bracket, recipes };
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const resolved = await resolve(params);
  if (!resolved) return {};
  const { craftType, bracket, recipes } = resolved;
  const job = CRAFT_TYPE_INFO[craftType].fullName;
  const title = `${bracket.label} ${job} Recipes (${bracket.expansion})`;
  const description = `The ${recipes.length.toLocaleString("en-US")} Final Fantasy XIV ${job} recipes from level ${bracket.min} to ${bracket.max} (${bracket.expansion}), each with its ingredient tree, shopping list, crafting order and a solver macro for your stats.`;
  return {
    title,
    description,
    alternates: { canonical: recipesByJobPath(craftType, bracket.slug) },
    openGraph: { title, description },
  };
}

export default async function JobBracketHubPage({ params }: Props) {
  const resolved = await resolve(params);
  if (!resolved) notFound();
  const { craftType, bracket, recipes } = resolved;
  const info = CRAFT_TYPE_INFO[craftType];

  return (
    <div className="p-4 space-y-6 max-w-5xl mx-auto">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{
          __html: breadcrumbListJsonLd([
            { name: "Home", path: "/" },
            { name: "Recipes", path: "/recipes" },
            { name: info.fullName, path: recipesByJobPath(craftType) },
            { name: bracket.label, path: recipesByJobPath(craftType, bracket.slug) },
          ]),
        }}
      />
      <SetBreadcrumbs
        items={[
          { label: "Recipes", href: "/recipes" },
          { label: info.fullName, href: recipesByJobPath(craftType) },
          { label: bracket.label },
        ]}
      />

      <header>
        <h1 className="text-2xl font-bold text-text-primary">
          <span className={info.colorClass}>{info.fullName}</span> Recipes {bracket.label}
        </h1>
        <p className="text-sm text-text-secondary mt-1">
          {recipes.length.toLocaleString("en-US")} {bracket.expansion} recipe{recipes.length === 1 ? "" : "s"} for{" "}
          {info.fullName}, level {bracket.min} to {bracket.max}.
        </p>
      </header>

      <nav aria-label="Other level ranges">
        <ul className="flex flex-wrap gap-2">
          {LEVEL_BRACKETS.map((other) => (
            <li key={other.slug}>
              <Link
                href={recipesByJobPath(craftType, other.slug)}
                aria-current={other === bracket ? "page" : undefined}
                className={`inline-block px-3 py-1.5 text-sm rounded-full border transition-colors ${
                  other === bracket
                    ? "bg-gold/15 border-gold text-gold"
                    : "bg-bg-surface border-border text-text-secondary hover:border-border-focus hover:text-text-primary"
                }`}
              >
                {other.label}
              </Link>
            </li>
          ))}
        </ul>
      </nav>

      <RecipeHubList recipes={recipes} />
    </div>
  );
}
