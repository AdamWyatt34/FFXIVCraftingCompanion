import type { EquipmentDto, GearSetDto, GearSlotDto, GearStatsDto, StatWeightProfileDto } from "@/types/gear";
import type { GearJobExport, SearchIndexEntry, StatWeightProfileExport } from "./types";
import { combatRole, hasOffhand, isCombat, isDoH, isDoL, jobDisplayName } from "./jobs";

// Port of GearRecommendationService + GearMapper (recommendations, profiles) and
// SearchEquipmentQueryHandler over the per-job candidate frontier. Proven by
// goldens/gear-sets.jsonl, gear-profile-lists.jsonl and gear-search.jsonl.

// CalculateScore adds the 21 stats in this order; summing in the same order keeps the doubles
// identical to the API's.
const STAT_KEYS = [
  "craftsmanship", "control", "cp", "gathering", "perception", "gp",
  "vitality", "strength", "dexterity", "intelligence", "mind", "piety", "tenacity",
  "directHitRate", "criticalHit", "determination", "skillSpeed", "spellSpeed",
  "physicalDamage", "defense", "magicDefense",
] as const;

type StatKey = (typeof STAT_KEYS)[number];

export const CANDIDATES_PER_SLOT = 10;
export const SEARCH_LIMIT = 50;

export function toProfileDto(profile: StatWeightProfileExport): StatWeightProfileDto {
  return { name: profile.name, displayName: profile.displayName, description: profile.description };
}

// StatWeightProfiles.GetProfile: case-insensitive, unknown names fall back to Balanced.
function getProfile(profiles: StatWeightProfileExport[], name: string): StatWeightProfileExport {
  const wanted = name.toUpperCase();
  return profiles.find((p) => p.name.toUpperCase() === wanted) ?? byName(profiles, "Balanced");
}

function byName(profiles: StatWeightProfileExport[], name: string): StatWeightProfileExport {
  const profile = profiles.find((p) => p.name === name);
  if (!profile) throw new Error(`Stat weight profile ${name} is missing from the data set.`);
  return profile;
}

const w = (p: StatWeightProfileExport, key: StatKey) => p.weights[key] ?? 0;

export function resolveProfile(profiles: StatWeightProfileExport[], job: string, profileName: string): StatWeightProfileExport {
  const profile = getProfile(profiles, profileName);
  if (isDoL(job)) {
    if (!(w(profile, "gathering") > 0 || w(profile, "perception") > 0 || w(profile, "gp") > 0))
      return byName(profiles, "GatheringBalanced");
  } else if (isDoH(job)) {
    if (!(w(profile, "craftsmanship") > 0 || w(profile, "control") > 0 || w(profile, "cp") > 0))
      return byName(profiles, "Balanced");
  } else if (isCombat(job)) {
    const combat =
      w(profile, "vitality") > 0 || w(profile, "strength") > 0 || w(profile, "dexterity") > 0 ||
      w(profile, "intelligence") > 0 || w(profile, "mind") > 0 || w(profile, "physicalDamage") > 0;
    if (!combat) return byName(profiles, roleProfileName(job));
  }
  return profile;
}

function roleProfileName(job: string): string {
  switch (combatRole(job)) {
    case "Healer": return "HealerBalanced";
    case "MeleeDPS": return "MeleeDPSBalanced";
    case "RangedDPS": return "RangedDPSBalanced";
    case "MagicDPS": return "MagicDPSBalanced";
    default: return "TankBalanced";
  }
}

/** GearRecommendationService.GetAvailableProfiles, used by GET /api/gear/profiles. */
export function availableProfiles(profiles: StatWeightProfileExport[], job: string): StatWeightProfileDto[] {
  const pick = (names: string[]) => names.map((n) => toProfileDto(byName(profiles, n)));
  if (isDoL(job)) return pick(["GatheringBalanced", "PerceptionFocused"]);
  if (isCombat(job)) return pick([roleProfileName(job)]);
  return pick(["Balanced", "ProgressFocused", "QualityFocused", "CpFocused"]);
}

function slotsForJob(job: string): { slot: string; label: string }[] {
  const slots = [{ slot: "MainHand", label: "Main Hand" }];
  if (hasOffhand(job)) slots.push({ slot: "OffHand", label: "Off Hand" });
  slots.push(
    { slot: "Head", label: "Head" },
    { slot: "Body", label: "Body" },
    { slot: "Hands", label: "Hands" },
    { slot: "Legs", label: "Legs" },
    { slot: "Feet", label: "Feet" },
    { slot: "Earring", label: "Earring" },
    { slot: "Necklace", label: "Necklace" },
    { slot: "Bracelet", label: "Bracelet" },
    { slot: "Ring", label: "Ring 1" },
    { slot: "Ring", label: "Ring 2" },
  );
  return slots;
}

function score(stats: GearStatsDto, profile: StatWeightProfileExport): number {
  let total = 0;
  for (const key of STAT_KEYS) total += stats[key] * w(profile, key);
  return total;
}

/** GearMapper.ToDto(equipment): an alternative carries no source and no score. */
function withoutScore(dto: EquipmentDto): EquipmentDto {
  return { ...dto, stats: { ...dto.stats, totalWeightedScore: 0 }, source: null, weightedScore: 0 };
}

export function recommendGear(
  jobFile: GearJobExport,
  profiles: StatWeightProfileExport[],
  level: number,
  profileName: string,
  craftedOnly: boolean,
): GearSetDto {
  const job = jobFile.job;
  const profile = resolveProfile(profiles, job, profileName);
  const slots = slotsForJob(job);

  const candidates = (slot: string): EquipmentDto[] => {
    let pool = (jobFile.candidatesBySlot[slot] ?? []).filter((e) => e.equipLevel <= level);
    if (craftedOnly) pool = pool.filter((e) => e.source?.type === "Crafted");
    return pool.slice(0, CANDIDATES_PER_SLOT);
  };

  const recommendations: GearSlotDto[] = [];
  const picked: EquipmentDto[] = [];
  let firstRingId: number | null = null;

  for (const { slot, label } of slots) {
    let scored = candidates(slot).map((e) => ({ equipment: e, score: score(e.stats, profile) }));
    scored.sort((a, b) => b.score - a.score);
    if (slot === "Ring" && firstRingId !== null) scored = scored.filter((x) => x.equipment.equipmentId !== firstRingId);

    const best = scored[0];
    const alternatives = scored.slice(1, 4).map((x) => withoutScore(x.equipment));
    const recommendation = best
      ? { ...best.equipment, stats: { ...best.equipment.stats, totalWeightedScore: best.score }, weightedScore: best.score }
      : null;

    recommendations.push({ slotName: label, slotKey: slot, recommendation, alternatives, isEmpty: recommendation === null });
    if (best) picked.push(best.equipment);
    if (slot === "Ring" && firstRingId === null && best) firstRingId = best.equipment.equipmentId;
  }

  const totals = Object.fromEntries(STAT_KEYS.map((k) => [k, 0])) as Record<StatKey, number>;
  for (const equipment of picked) for (const key of STAT_KEYS) totals[key] += equipment.stats[key];
  const totalStats: GearStatsDto = { ...totals, totalWeightedScore: score({ ...totals, totalWeightedScore: 0 }, profile) };

  return {
    job,
    jobName: jobDisplayName(job),
    level,
    profile: toProfileDto(profile),
    slots: recommendations,
    totalStats,
  };
}

export interface GearSearchParams {
  query: string;
  job?: string | null;
  slot?: string | null;
  minLevel?: number | null;
  maxLevel?: number | null;
}

function foldAscii(s: string): string {
  return s.replace(/[A-Z]/g, (c) => c.toLowerCase());
}

/** SearchEquipmentAsync over the search index: the matching equipment ids, best item level first. */
export function searchGearIndex(index: readonly SearchIndexEntry[], params: GearSearchParams): SearchIndexEntry[] {
  const needle = params.query.trim() === "" ? null : foldAscii(params.query);
  const slot = params.slot ? params.slot.toUpperCase() : null;
  return index
    .filter((e) => e.equipment !== null)
    .filter((e) => needle === null || foldAscii(e.name).includes(needle))
    .filter((e) => !params.job || e.equipment!.classJobCategory.includes(params.job))
    .filter((e) => slot === null || e.equipment!.slot.toUpperCase() === slot)
    .filter((e) => params.minLevel == null || e.equipment!.equipLevel >= params.minLevel)
    .filter((e) => params.maxLevel == null || e.equipment!.equipLevel <= params.maxLevel)
    .sort((a, b) => b.itemLevel - a.itemLevel || a.itemId - b.itemId)
    .slice(0, SEARCH_LIMIT);
}

export const EQUIP_SLOTS = ["Head", "Body", "Hands", "Legs", "Feet", "MainHand", "OffHand", "Earring", "Necklace", "Bracelet", "Ring"] as const;

export function isEquipSlot(slot: string): boolean {
  return EQUIP_SLOTS.some((s) => s.toUpperCase() === slot.toUpperCase());
}

export { withoutScore as toSearchResultDto };
