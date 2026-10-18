import Link from "next/link";
import { Map, ShoppingCart } from "lucide-react";
import { ItemIcon } from "@/components/ui/ItemIcon";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import type { RecipeDto, CraftType } from "@/types/api";
import type { CrafterStatsInput } from "@/types/simulator";
import { recipeJourneyPath, recipePath } from "@/lib/routes";

interface RecipeParamsProps {
  recipe: RecipeDto;
  stats: CrafterStatsInput | null;
}

function getStatColor(
  statValue: number | undefined,
  required: number,
): string {
  if (statValue === undefined || required === 0) return "text-text-primary";
  if (statValue >= required) return "text-emerald-400";
  if (statValue >= required * 0.9) return "text-amber-400";
  return "text-red-400";
}

function getStatHint(
  statValue: number | undefined,
  required: number,
): string | null {
  if (statValue === undefined || required === 0) return null;
  if (statValue >= required) return `Your ${statValue.toLocaleString()} meets the requirement`;
  const diff = required - statValue;
  return `${diff.toLocaleString()} short of requirement`;
}

export function RecipeParams({ recipe, stats }: RecipeParamsProps) {
  const craftInfo = CRAFT_TYPE_INFO[recipe.craftType as CraftType] ?? null;

  return (
    <div className="bg-bg-surface rounded-lg border border-border p-4 animate-fade-in">
      {/* Header */}
      <div className="flex items-center gap-3 mb-4">
        <ItemIcon
          iconUrl={recipe.resultItem.iconUrl}
          alt={recipe.resultItem.name}
          size="md"
        />
        <div>
          <h3 className="font-bold text-text-primary">
            {recipe.resultItem.name}
          </h3>
          <div className="flex items-center gap-2 mt-0.5">
            {craftInfo && (
              <span
                className={`text-xs px-2 py-0.5 rounded-full bg-bg-elevated ${craftInfo.colorClass}`}
              >
                {craftInfo.name}
              </span>
            )}
            <span className="text-sm text-text-secondary">
              Lv. {recipe.recipeLevel}
            </span>
          </div>
        </div>
      </div>

      {/* Parameters grid */}
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
        <ParamCell label="Difficulty" value={recipe.difficulty} />
        <ParamCell label="Quality" value={recipe.quality} />
        <ParamCell label="Durability" value={recipe.durability} />
        <ParamCell
          label="Min Craft"
          value={recipe.requiredCraftsmanship}
          colorClass={getStatColor(
            stats?.craftsmanship,
            recipe.requiredCraftsmanship,
          )}
          hint={getStatHint(
            stats?.craftsmanship,
            recipe.requiredCraftsmanship,
          )}
        />
        <ParamCell
          label="Min Control"
          value={recipe.requiredControl}
          colorClass={getStatColor(stats?.control, recipe.requiredControl)}
          hint={getStatHint(stats?.control, recipe.requiredControl)}
        />
      </div>

      {/* Navigation links */}
      <div className="flex flex-wrap gap-2 mt-3 pt-3 border-t border-border">
        <Link
          href={recipePath(recipe.recipeId, recipe.resultItem.name)}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md bg-bg-elevated text-text-secondary hover:text-gold transition-colors"
        >
          <ShoppingCart size={14} />
          View Materials
        </Link>
        <Link
          href={recipeJourneyPath(recipe.recipeId, recipe.resultItem.name)}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md bg-gold/15 text-gold hover:bg-gold/25 transition-colors"
        >
          <Map size={14} />
          Start Journey
        </Link>
      </div>
    </div>
  );
}

function ParamCell({
  label,
  value,
  colorClass = "text-text-primary",
  hint,
}: {
  label: string;
  value: number;
  colorClass?: string;
  hint?: string | null;
}) {
  return (
    <div className="bg-bg-elevated rounded-md p-2 text-center" title={hint ?? undefined}>
      <p className={`text-lg font-bold ${colorClass}`}>
        {value.toLocaleString()}
      </p>
      <p className="text-xs text-text-muted">{label}</p>
    </div>
  );
}
