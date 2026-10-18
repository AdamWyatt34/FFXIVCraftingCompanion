import Link from "next/link";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import { CRAFT_JOBS } from "@/lib/static-data/hubs";
import { QUICK_FILTERS } from "@/lib/static-data/search";
import { DAWNTRAIL_RECIPES_PATH, recipeFilterPath, recipesByJobPath } from "@/lib/routes";
import type { CraftType } from "@/types/api";

/**
 * The browse links under the recipe search: the job hubs and the quick filters, rendered on the
 * server so they are in the HTML whatever the search state (the search itself hydrates later).
 */
export function RecipeBrowse({ counts }: { counts: Record<CraftType, number> }) {
  return (
    <section className="p-4 border-t border-border space-y-4" aria-labelledby="browse-by-job">
      <div>
        <h2 id="browse-by-job" className="text-sm font-semibold text-text-secondary uppercase tracking-wider mb-2">
          Browse by job
        </h2>
        <ul className="flex flex-wrap gap-2">
          {CRAFT_JOBS.map((job) => {
            const info = CRAFT_TYPE_INFO[job];
            return (
              <li key={job}>
                <Link
                  href={recipesByJobPath(job)}
                  className={`inline-flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-full bg-bg-surface border border-border hover:border-border-focus transition-colors ${info.colorClass}`}
                >
                  {info.fullName}
                  <span className="text-xs text-text-muted">{counts[job].toLocaleString("en-US")}</span>
                </Link>
              </li>
            );
          })}
        </ul>
      </div>
      <div>
        <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider mb-2">Quick filters</h2>
        <ul className="flex flex-wrap gap-2">
          <li>
            <Link href={DAWNTRAIL_RECIPES_PATH} className="inline-block px-3 py-1.5 text-sm rounded-full bg-bg-elevated text-text-secondary border border-border hover:border-border-focus hover:text-text-primary transition-colors">
              {QUICK_FILTERS.dawntrail.label}
            </Link>
          </li>
          <li>
            <Link href={recipeFilterPath({ quickFilter: "level100Gear" })} className="inline-block px-3 py-1.5 text-sm rounded-full bg-bg-elevated text-text-secondary border border-border hover:border-border-focus hover:text-text-primary transition-colors">
              {QUICK_FILTERS.level100Gear.label}
            </Link>
          </li>
          <li>
            <Link href={recipeFilterPath({ quickFilter: "materials" })} className="inline-block px-3 py-1.5 text-sm rounded-full bg-bg-elevated text-text-secondary border border-border hover:border-border-focus hover:text-text-primary transition-colors">
              {QUICK_FILTERS.materials.label}
            </Link>
          </li>
        </ul>
      </div>
    </section>
  );
}
