import type { Metadata } from "next";
import Link from "next/link";
import { listRecipesServer } from "@/lib/static-data/server";
import { CRAFT_JOBS, LATEST_BRACKET, recipesForJob, recipesInBracket, sortForHub } from "@/lib/static-data/hubs";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import { DAWNTRAIL_RECIPES_PATH, recipesByJobPath } from "@/lib/routes";
import { breadcrumbListJsonLd } from "@/lib/structured-data";
import { SetBreadcrumbs } from "@/components/layout/SetBreadcrumbs";
import { RecipeHubList } from "@/components/recipes/RecipeHubList";

const TITLE = `${LATEST_BRACKET.expansion} Crafting Recipes (${LATEST_BRACKET.label})`;
const DESCRIPTION =
  "Every Final Fantasy XIV Dawntrail crafting recipe, level 91 to 100, for all eight Disciples of the Hand, with ingredient trees, shopping lists and solver macros.";

export const metadata: Metadata = {
  title: TITLE,
  description: DESCRIPTION,
  alternates: { canonical: DAWNTRAIL_RECIPES_PATH },
  openGraph: { title: TITLE, description: DESCRIPTION },
};

/** How many of each job's newest recipes the hub shows before handing over to the job's own page. */
const PREVIEW_PER_JOB = 12;

export default async function DawntrailHubPage() {
  const recipes = await listRecipesServer();
  const perJob = CRAFT_JOBS.map((job) => {
    const all = sortForHub(recipesInBracket(recipesForJob(recipes, job), LATEST_BRACKET));
    return { job, total: all.length, preview: all.slice(-PREVIEW_PER_JOB).reverse() };
  });
  const total = perJob.reduce((sum, entry) => sum + entry.total, 0);

  return (
    <div className="p-4 space-y-8 max-w-5xl mx-auto">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{
          __html: breadcrumbListJsonLd([
            { name: "Home", path: "/" },
            { name: "Recipes", path: "/recipes" },
            { name: `${LATEST_BRACKET.expansion} Recipes`, path: DAWNTRAIL_RECIPES_PATH },
          ]),
        }}
      />
      <SetBreadcrumbs items={[{ label: "Recipes", href: "/recipes" }, { label: LATEST_BRACKET.expansion }]} />

      <header>
        <h1 className="text-2xl font-bold text-text-primary">{LATEST_BRACKET.expansion} Crafting Recipes</h1>
        <p className="text-sm text-text-secondary mt-1">
          {total.toLocaleString("en-US")} recipe{total === 1 ? "" : "s"} from level {LATEST_BRACKET.min} to {LATEST_BRACKET.max} across the eight
          crafting jobs. The newest recipes of every job are below; the full lists are on the job pages.
        </p>
      </header>

      <nav aria-label="Dawntrail recipes by job">
        <ul className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          {perJob.map(({ job, total: count }) => (
            <li key={job}>
              <Link
                href={recipesByJobPath(job, LATEST_BRACKET.slug)}
                className="block p-3 rounded-lg bg-bg-surface border border-border hover:border-gold transition-colors"
              >
                <span className={`block text-sm font-medium ${CRAFT_TYPE_INFO[job].colorClass}`}>{CRAFT_TYPE_INFO[job].fullName}</span>
                <span className="block text-xs text-text-muted mt-0.5">
                  {count.toLocaleString("en-US")} recipe{count === 1 ? "" : "s"}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      </nav>

      {perJob
        .filter(({ total: count }) => count > 0)
        .map(({ job, total: count, preview }) => (
          <section key={job} aria-labelledby={`job-${job}`} className="space-y-4">
            <div className="flex items-baseline justify-between gap-3">
              <h2 id={`job-${job}`} className={`text-lg font-semibold ${CRAFT_TYPE_INFO[job].colorClass}`}>
                {CRAFT_TYPE_INFO[job].fullName}
              </h2>
              <Link
                href={recipesByJobPath(job, LATEST_BRACKET.slug)}
                className="text-sm text-gold hover:text-gold-light transition-colors"
              >
                All {count.toLocaleString("en-US")} {LATEST_BRACKET.label} recipes
              </Link>
            </div>
            <RecipeHubList recipes={preview} />
          </section>
        ))}
    </div>
  );
}
