import type { Metadata } from "next";
import Link from "next/link";
import { notFound, permanentRedirect } from "next/navigation";
import { FlaskConical, Map, Shield, Sparkles } from "lucide-react";
import { getCrystalsServer } from "@/lib/static-data/server";
import { directIngredients } from "@/lib/static-data/materials";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import { itemPath, recipeJourneyPath, recipePath, recipesByJobPath, simulatorPath } from "@/lib/routes";
import { breadcrumbListJsonLd } from "@/lib/structured-data";
import { resolveRecipeRoute } from "./resolve";
import { ItemIcon, StatBar } from "@/components/ui";
import {
  RecipeBackLink,
  RecipeCraftability,
  RecipeFavoriteButton,
  RecipeMaterials,
} from "@/components/recipes";
import type { CraftType } from "@/types/api";

interface Props {
  params: Promise<{ slug: string }>;
}

// Rendered on first request from the exported data and cached for the deployment's lifetime.
// Prerendering all ~14k recipes measured 1.4 GB of build output (about 100 KB a page), too much
// to upload on every deploy for pages that are static anyway; the API is never involved.
export const dynamicParams = true;
export const revalidate = false;

export function generateStaticParams(): { slug: string }[] {
  return [];
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const route = await resolveRecipeRoute(slug);

  // The page itself answers the 404 and the 308; neither response carries metadata.
  if (route.status !== "ok") {
    return { title: "Recipe Not Found", robots: { index: false, follow: false } };
  }

  const { recipe, craftingParameters } = route.data;
  const name = recipe.resultItem.name;
  const job = CRAFT_TYPE_INFO[recipe.craftType as CraftType]?.fullName ?? recipe.craftType;
  const stars = craftingParameters.stars > 0 ? ` ${"★".repeat(craftingParameters.stars)}` : "";
  const title = `${name} Recipe – FFXIV ${job} Lv. ${craftingParameters.classJobLevel}${stars}`;
  const top = recipe.ingredients.slice(0, 3).map((i) => `${i.itemName} ×${i.quantity}`).join(", ");
  const more = recipe.ingredients.length > 3 ? ", …" : "";
  const description =
    `How to craft ${name} in Final Fantasy XIV: ${job} level ${craftingParameters.classJobLevel} recipe` +
    (recipe.ingredients.length > 0 ? ` needing ${top}${more}.` : ".") +
    " Material tree, shopping list, crafting order and a solver macro for your stats.";

  return {
    title,
    description,
    alternates: { canonical: recipePath(recipe.recipeId, name) },
    openGraph: { title, description, type: "article" },
    twitter: { card: "summary_large_image", title, description },
  };
}

export default async function RecipeDetailPage({ params }: Props) {
  const { slug } = await params;
  const route = await resolveRecipeRoute(slug);
  if (route.status === "not-found") notFound();
  // A bare id or a stale name 308s to the canonical slug; the query string is not carried over.
  if (route.status === "redirect") permanentRedirect(recipePath(route.recipeId, route.itemName));

  const { recipe, craftingParameters, materials } = route.data;
  const name = recipe.resultItem.name;
  const craftInfo = CRAFT_TYPE_INFO[recipe.craftType as CraftType];
  const jobName = craftInfo?.fullName ?? recipe.craftType;
  const ingredients = directIngredients(recipe.ingredients, materials, await getCrystalsServer());
  const equipInfo = recipe.equipmentInfo;
  const breadcrumbs = breadcrumbListJsonLd([
    { name: "Home", path: "/" },
    { name: "Recipes", path: "/recipes" },
    ...(craftInfo ? [{ name: jobName, path: recipesByJobPath(recipe.craftType as CraftType) }] : []),
    { name, path: recipePath(recipe.recipeId, name) },
  ]);

  return (
    <div className="p-4 space-y-6 max-w-2xl mx-auto">
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: breadcrumbs }} />

      <RecipeBackLink />

      {/* Header: everything a search result needs is in the HTML. */}
      <header className="flex items-start gap-4">
        <ItemIcon iconUrl={recipe.resultItem.iconUrl} alt={name} size="2xl" />
        <div className="flex-1 min-w-0">
          <h1 className="text-xl font-bold text-text-primary">{name}</h1>
          <div className="flex items-center gap-2 mt-1 flex-wrap">
            <span className={`text-sm font-medium ${craftInfo?.colorClass ?? "text-text-primary"}`}>
              {jobName}
            </span>
            <span className="text-text-muted text-sm">
              Lv. {craftingParameters.classJobLevel}
              {craftingParameters.stars > 0 && (
                <span className="text-gold ml-1" aria-label={`${craftingParameters.stars} star`}>
                  {"★".repeat(craftingParameters.stars)}
                </span>
              )}
            </span>
            <span className="text-text-muted text-sm">iLv {recipe.resultItem.itemLevel}</span>
            {recipe.yield > 1 && (
              <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-gold/20 text-gold">
                Yields {recipe.yield}
              </span>
            )}
          </div>
          {recipe.materialSummary && (
            <div className="flex items-center gap-2 mt-2 flex-wrap">
              <span className="text-xs text-text-muted bg-bg-elevated px-2 py-0.5 rounded">
                {recipe.materialSummary.rawMaterialCount} materials
              </span>
              <span className="text-xs text-text-muted bg-bg-elevated px-2 py-0.5 rounded">
                {recipe.materialSummary.crystalTypeCount} crystal types
              </span>
              {recipe.materialSummary.intermediateCraftCount > 0 && (
                <span className="text-xs text-text-muted bg-bg-elevated px-2 py-0.5 rounded">
                  {recipe.materialSummary.intermediateCraftCount} sub-crafts
                </span>
              )}
            </div>
          )}
        </div>
        <RecipeFavoriteButton recipeId={recipe.recipeId} />
      </header>

      <RecipeCraftability
        recipe={{
          craftType: recipe.craftType,
          recipeLevel: craftingParameters.classJobLevel,
          requiredCraftsmanship: recipe.requiredCraftsmanship,
          requiredControl: recipe.requiredControl,
        }}
      />

      {/* Recipe stats - 2 column on desktop */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <section className="bg-bg-surface rounded-lg p-4 space-y-3 border border-border">
          <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
            Recipe Stats
          </h2>
          <StatBar label="Difficulty" value={recipe.difficulty} />
          <StatBar label="Quality" value={recipe.quality} />
          <StatBar label="Durability" value={recipe.durability} />
        </section>
        <section className="bg-bg-surface rounded-lg p-4 space-y-3 border border-border">
          <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
            Required Stats
          </h2>
          <StatBar label="Craftsmanship" value={recipe.requiredCraftsmanship} />
          <StatBar label="Control" value={recipe.requiredControl} />
        </section>
      </div>

      {/* Journey button */}
      <Link
        href={recipeJourneyPath(recipe.recipeId, name)}
        className="flex items-center gap-2 p-3 rounded-lg bg-gold/10 border border-gold/30 hover:bg-gold/20 transition-colors"
      >
        <Map className="text-gold shrink-0" size={18} />
        <span className="text-sm font-medium text-gold">Start Crafting Journey</span>
      </Link>

      {/* Simulate + Auto-Solve links */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <Link
          href={simulatorPath(recipe.recipeId)}
          className="flex items-center gap-2 p-3 rounded-lg bg-bg-surface border border-border hover:border-gold transition-colors"
        >
          <FlaskConical className="text-gold shrink-0" size={18} />
          <span className="text-sm text-text-primary">Simulate this Recipe</span>
        </Link>
        <Link
          href={simulatorPath(recipe.recipeId, { solve: true })}
          className="flex items-center gap-2 p-3 rounded-lg bg-bg-surface border border-border hover:border-gold transition-colors"
        >
          <Sparkles className="text-gold shrink-0" size={18} />
          <span className="text-sm text-text-primary">Auto-Solve this Recipe</span>
        </Link>
      </div>

      {/* Equipment info banner */}
      {equipInfo && (
        <section className="rounded-lg border border-gold/30 bg-gold/5 p-4 space-y-3">
          <div className="flex items-center gap-2">
            <Shield size={18} className="text-gold" />
            <span className="text-sm font-medium text-gold">
              This is gear for {equipInfo.classJobCategory}
            </span>
          </div>
          <div className="grid grid-cols-3 gap-2">
            {(
              [
                ["Craftsmanship", equipInfo.craftsmanship],
                ["Control", equipInfo.control],
                ["CP", equipInfo.cp],
                ["Gathering", equipInfo.gathering],
                ["Perception", equipInfo.perception],
                ["GP", equipInfo.gp],
              ] as const
            )
              .filter(([, value]) => value > 0)
              .map(([label, value]) => (
                <div key={label} className="bg-bg-elevated rounded-md p-2 text-center">
                  <p className="text-sm font-bold text-text-primary">{value}</p>
                  <p className="text-xs text-text-muted">{label}</p>
                </div>
              ))}
          </div>
          <Link
            href={`/gear?job=${encodeURIComponent(equipInfo.classJobCategory.split(",")[0]?.trim() ?? "")}&level=${recipe.resultItem.itemLevel}&mode=recommend`}
            className="inline-flex items-center gap-1.5 text-sm text-gold hover:text-gold-light transition-colors"
          >
            <Shield size={14} />
            Compare with recommendations
          </Link>
        </section>
      )}

      {/* Direct ingredients, with the first level of the material tree under crafted ones */}
      <section className="bg-bg-surface rounded-lg p-4 border border-border">
        <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider mb-3">
          Ingredients ({ingredients.length})
        </h2>
        {ingredients.length === 0 ? (
          <p className="text-sm text-text-muted py-2">This recipe has no ingredients.</p>
        ) : (
          <ul className="space-y-2">
            {ingredients.map((ingredient) => (
              <li
                key={ingredient.itemId}
                className="p-2 rounded-lg hover:bg-bg-elevated transition-colors"
              >
                <div className="flex items-center gap-3">
                  <ItemIcon iconUrl={ingredient.iconUrl} alt="" size="md" />
                  <Link
                    href={itemPath(ingredient.itemId)}
                    className="flex-1 min-w-0 text-sm text-text-primary hover:text-gold transition-colors truncate"
                  >
                    {ingredient.itemName}
                  </Link>
                  <span className="text-xs text-text-muted">{ingredient.primarySource}</span>
                  <div className="flex items-center gap-2 shrink-0">
                    {ingredient.canBeHQ && (
                      <span title="Can be HQ">
                        <Sparkles size={14} className="text-gold" />
                      </span>
                    )}
                    <span className="text-sm text-text-muted font-mono">×{ingredient.quantity}</span>
                  </div>
                </div>
                {ingredient.madeFrom.length > 0 && (
                  <p className="mt-1 ml-11 text-xs text-text-muted">
                    Made from:{" "}
                    {ingredient.madeFrom.map((part, index) => (
                      <span key={part.itemId}>
                        {index > 0 && ", "}
                        <Link href={itemPath(part.itemId)} className="hover:text-gold transition-colors">
                          {part.itemName}
                        </Link>{" "}
                        ×{part.quantity}
                      </span>
                    ))}
                  </p>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* Quantity, shopping list, crafting tree and steps: interactive, from the cached export. */}
      <RecipeMaterials
        recipeId={recipe.recipeId}
        itemName={name}
        equipInfo={equipInfo}
      />
    </div>
  );
}
