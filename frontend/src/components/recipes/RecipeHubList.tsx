import Link from "next/link";
import { ItemIcon } from "@/components/ui/ItemIcon";
import { recipePath } from "@/lib/routes";
import { groupByLevel, type HubRecipe } from "@/lib/static-data/hubs";

/** A hub page's recipes, grouped by level, every one a plain link: this is what a crawler indexes. */
export function RecipeHubList({ recipes }: { recipes: readonly HubRecipe[] }) {
  if (recipes.length === 0) {
    return <p className="text-sm text-text-muted">No recipes in this range in the current data set.</p>;
  }
  return (
    <div className="space-y-6">
      {groupByLevel(recipes).map(({ level, recipes: group }) => (
        <section key={level} aria-labelledby={`level-${level}`}>
          <h2 id={`level-${level}`} className="text-sm font-semibold text-text-secondary uppercase tracking-wider mb-2">
            Level {level}
            <span className="ml-2 normal-case tracking-normal font-normal text-text-muted">
              {group.length} recipe{group.length === 1 ? "" : "s"}
            </span>
          </h2>
          <ul className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            {group.map((recipe) => (
              <li key={recipe.recipeId}>
                <Link
                  href={recipePath(recipe.recipeId, recipe.itemName)}
                  className="flex items-center gap-3 p-3 rounded-lg bg-bg-surface hover:bg-bg-elevated border border-border hover:border-border-focus transition-colors"
                >
                  <ItemIcon iconUrl={recipe.iconUrl} alt="" size="md" />
                  <span className="flex-1 min-w-0">
                    <span className="block text-sm font-medium text-text-primary truncate">{recipe.itemName}</span>
                    <span className="block text-xs text-text-muted mt-0.5">
                      Lv. {recipe.classJobLevel}
                      {recipe.stars > 0 && <span className="text-gold ml-1">{"★".repeat(recipe.stars)}</span>}
                      {" · "}iLv {recipe.itemLevel}
                    </span>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}
