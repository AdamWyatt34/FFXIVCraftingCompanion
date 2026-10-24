import Link from "next/link";
import { Map, ShoppingCart } from "lucide-react";
import { ItemIcon } from "@/components/ui/ItemIcon";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import type { RecipeDto, CraftType } from "@/types/api";
import type { CrafterStatsInput } from "@/types/simulator";
import type { CraftingParameters } from "@/lib/static-data/recipes";
import { recipeJourneyPath, recipePath } from "@/lib/routes";

type RecipeStatTargets = Pick<
  CraftingParameters,
  "requiredCraftsmanship" | "requiredControl" | "suggestedCraftsmanship" | "suggestedControl"
>;

interface RecipeParamsProps {
  recipe: RecipeDto;
  stats: CrafterStatsInput | null;
  /** Null until the recipe's crafting parameters load. */
  targets: RecipeStatTargets | null;
}

type TargetKind = "recommended" | "required";

const BELOW_CLASS: Record<TargetKind, string> = {
  recommended: "text-amber-400",
  required: "text-red-400",
};

function getStatColor(
  statValue: number | undefined,
  target: number,
  kind: TargetKind,
): string {
  if (statValue === undefined || target === 0) return "text-text-primary";
  return statValue >= target ? "text-emerald-400" : BELOW_CLASS[kind];
}

function getStatHint(
  statValue: number | undefined,
  target: number,
  kind: TargetKind,
): string | null {
  if (statValue === undefined || target === 0) return null;
  if (statValue >= target) return `Your ${statValue.toLocaleString()} meets the ${kind} value`;
  const diff = target - statValue;
  return `${diff.toLocaleString()} short of the ${kind} value`;
}

function TargetCell({
  label,
  statValue,
  target,
  kind,
}: {
  label: string;
  statValue: number | undefined;
  target: number;
  kind: TargetKind;
}) {
  return (
    <ParamCell
      label={label}
      value={target}
      colorClass={getStatColor(statValue, target, kind)}
      hint={getStatHint(statValue, target, kind)}
    />
  );
}

export function RecipeParams({ recipe, stats, targets }: RecipeParamsProps) {
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
              Recipe Lv. {recipe.recipeLevel}
            </span>
          </div>
        </div>
      </div>

      {/* Parameters grid */}
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
        <ParamCell label="Difficulty" value={recipe.difficulty} />
        <ParamCell label="Quality" value={recipe.quality} />
        <ParamCell label="Durability" value={recipe.durability} />
        {targets && (
          <>
            <TargetCell
              label="Recommended Craft"
              statValue={stats?.craftsmanship}
              target={targets.suggestedCraftsmanship}
              kind="recommended"
            />
            <TargetCell
              label="Recommended Control"
              statValue={stats?.control}
              target={targets.suggestedControl}
              kind="recommended"
            />
            {targets.requiredCraftsmanship > 0 && (
              <TargetCell
                label="Required Craft"
                statValue={stats?.craftsmanship}
                target={targets.requiredCraftsmanship}
                kind="required"
              />
            )}
            {targets.requiredControl > 0 && (
              <TargetCell
                label="Required Control"
                statValue={stats?.control}
                target={targets.requiredControl}
                kind="required"
              />
            )}
          </>
        )}
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
