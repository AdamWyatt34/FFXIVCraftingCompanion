import type { RecipeCraftingParametersDto, RecipeDto } from "@/types/api";
import type { EquipmentDto, GearSetDto } from "@/types/gear";
import type { GearUpgradeSuggestionDto, StatRecommendation, StatSufficiencyResultDto } from "@/types/journey";
import type { CharacterProfileDto } from "@/types/profile";
import type { CrafterStatsInput } from "@/types/simulator";

// Port of StatSufficiencyService: the stat check a signed-in visitor sees on the journey page.
// The minimums, the recommendation and the gear suggestions are proven by goldens/sufficiency.jsonl
// (generated with a solver that returns no rotation, so the golden's HQ estimate is always zero);
// the solve and the simulation run in the WASM engine, whose parity with the C# engine the
// crate's own goldens prove.

export interface SufficiencyProfile {
  activeJob: string;
  activeJobLevel: number;
  /** Equipment id per profile slot key ("MainHand", "Ring1"); null when the profile keeps no gear. */
  currentGear: Record<string, number> | null;
}

export interface SolveOutcome {
  /** SolverStatus.Completed. */
  completed: boolean;
  actionIds: number[];
}

export interface SimulationOutcome {
  success: boolean;
  totalSteps: number;
  finalQualityPercent: number;
  finalHQPercent: number;
}

/** The engine calls the service makes: the greedy solver with default constraints, then a plain simulation. */
export interface SufficiencyEngine {
  solve(recipeId: number, stats: CrafterStatsInput): Promise<SolveOutcome>;
  simulate(recipeId: number, stats: CrafterStatsInput, actionIds: number[]): Promise<SimulationOutcome>;
}

/** The gear data the suggestions need. */
export interface GearSource {
  /** GetRecommendationsAsync(job, level, "balanced", craftedOnly: false). */
  recommend(job: string, level: number): Promise<GearSetDto>;
  /** IEquipmentRepository.GetByIdsAsync: ids that are not equipment are left out. */
  equipment(ids: number[]): Promise<Map<number, EquipmentDto>>;
}

/** GearMapper.MapSlotToProfileKey: profiles store gear under the slot keys, rings numbered. */
export function mapSlotToProfileKey(slotKey: string, slotName: string): string {
  return slotKey !== "Ring" ? slotKey : slotName === "Ring 1" ? "Ring1" : "Ring2";
}

export function determineRecommendation(meetsMinimums: boolean, hqPercent: number, canComplete: boolean): StatRecommendation {
  if (!meetsMinimums || !canComplete) return "UpgradeGear";
  if (hqPercent >= 80) return "StatsExcellent";
  if (hqPercent >= 60) return "StatsGood";
  return "StatsAdequate";
}

export async function analyzeSufficiency(
  recipe: RecipeDto,
  parameters: RecipeCraftingParametersDto,
  stats: CrafterStatsInput,
  profile: SufficiencyProfile | null,
  engine: SufficiencyEngine,
  gear: GearSource,
): Promise<StatSufficiencyResultDto> {
  const craftsmanshipSufficient = stats.craftsmanship >= recipe.requiredCraftsmanship;
  const controlSufficient = stats.control >= recipe.requiredControl;
  const levelSufficient = stats.level >= parameters.classJobLevel;
  const meetsMinimumRequirements = craftsmanshipSufficient && controlSufficient && levelSufficient;

  let estimatedHQPercent = 0;
  let estimatedQualityPercent = 0;
  let estimatedSteps = 0;
  let canComplete = false;

  if (meetsMinimumRequirements) {
    const solved = await engine.solve(recipe.recipeId, stats);
    if (solved.completed && solved.actionIds.length > 0) {
      const simulation = await engine.simulate(recipe.recipeId, stats, solved.actionIds);
      estimatedHQPercent = simulation.finalHQPercent;
      estimatedQualityPercent = simulation.finalQualityPercent;
      estimatedSteps = simulation.totalSteps;
      canComplete = simulation.success;
    }
  }

  const suggestedGearUpgrades =
    profile !== null && profile.currentGear !== null ? await gearUpgradeSuggestions(profile, gear) : [];

  return {
    recipeId: recipe.recipeId,
    meetsMinimumRequirements,
    craftsmanshipSufficient,
    controlSufficient,
    levelSufficient,
    craftsmanshipDeficit: craftsmanshipSufficient ? 0 : recipe.requiredCraftsmanship - stats.craftsmanship,
    controlDeficit: controlSufficient ? 0 : recipe.requiredControl - stats.control,
    levelDeficit: levelSufficient ? 0 : parameters.classJobLevel - stats.level,
    estimatedHQPercent,
    estimatedQualityPercent,
    estimatedSteps,
    canComplete,
    recommendation: determineRecommendation(meetsMinimumRequirements, estimatedHQPercent, canComplete),
    suggestedGearUpgrades,
  };
}

async function gearUpgradeSuggestions(profile: SufficiencyProfile, gear: GearSource): Promise<GearUpgradeSuggestionDto[]> {
  const worn = profile.currentGear ?? {};
  const recommended = await gear.recommend(profile.activeJob, profile.activeJobLevel);
  const slots = recommended.slots.filter((s) => s.recommendation !== null);

  const wornId = (slotKey: string, slotName: string): number | null => worn[mapSlotToProfileKey(slotKey, slotName)] ?? null;
  const currentIds = [...new Set(slots.map((s) => wornId(s.slotKey, s.slotName)).filter((id): id is number => id !== null && id !== 0))];
  const current = await gear.equipment(currentIds);

  const suggestions: GearUpgradeSuggestionDto[] = [];
  for (const slot of slots) {
    const suggested = slot.recommendation!;
    const base = {
      slot: slot.slotName,
      suggestedItem: suggested.name,
      suggestedEquipmentId: suggested.equipmentId,
      source: suggested.source?.type ?? null,
      recipeId: suggested.source?.recipeId ?? null,
    };

    const currentId = wornId(slot.slotKey, slot.slotName);
    if (currentId === null || currentId === 0) {
      suggestions.push({
        ...base,
        currentItem: null,
        craftsmanshipGain: suggested.stats.craftsmanship,
        controlGain: suggested.stats.control,
        cpGain: suggested.stats.cp,
      });
      continue;
    }

    if (currentId === suggested.equipmentId) continue;
    const equipped = current.get(currentId);
    if (!equipped) continue;

    const craftsmanshipGain = suggested.stats.craftsmanship - equipped.stats.craftsmanship;
    const controlGain = suggested.stats.control - equipped.stats.control;
    const cpGain = suggested.stats.cp - equipped.stats.cp;
    if (craftsmanshipGain <= 0 && controlGain <= 0 && cpGain <= 0) continue;

    suggestions.push({ ...base, currentItem: equipped.name, craftsmanshipGain, controlGain, cpGain });
  }
  return suggestions;
}

/** What the journey needs from a signed-in visitor. */
export interface JourneyViewer {
  stats: CrafterStatsInput;
  profile: SufficiencyProfile;
  engine: SufficiencyEngine;
}

/**
 * GetCraftingJourneyQuery reads the stats and the gear off the character profile. The profile DTO
 * carries no gear both when the profile has none and when its gear list is empty; the former
 * (no suggestions) is assumed, so a profile with no gear entered is not told to replace every slot.
 */
export function viewerFromProfile(profile: CharacterProfileDto, engine: SufficiencyEngine): JourneyViewer {
  return {
    stats: {
      level: profile.activeJobLevel,
      craftsmanship: profile.stats.craftsmanship,
      control: profile.stats.control,
      cp: profile.stats.cp,
    },
    profile: {
      activeJob: profile.activeJob,
      activeJobLevel: profile.activeJobLevel,
      currentGear: profile.currentGear
        ? Object.fromEntries(profile.currentGear.map((slot) => [slot.slotKey, slot.equipmentId]))
        : null,
    },
    engine,
  };
}
