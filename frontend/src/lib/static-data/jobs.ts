// Port of FFXIV.Domain.Constants.JobClassification (the lists the gear service keys on).

export const DOH_JOBS = ["CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL"] as const;
export const DOL_JOBS = ["MIN", "BTN", "FSH"] as const;
export const TANK_JOBS = ["PLD", "WAR", "DRK", "GNB"] as const;
export const HEALER_JOBS = ["WHM", "SCH", "AST", "SGE"] as const;
export const MELEE_DPS_JOBS = ["MNK", "DRG", "NIN", "SAM", "RPR", "VPR"] as const;
export const RANGED_DPS_JOBS = ["BRD", "MCH", "DNC"] as const;
export const MAGIC_DPS_JOBS = ["BLM", "SMN", "RDM", "PCT"] as const;
export const COMBAT_JOBS = [...TANK_JOBS, ...HEALER_JOBS, ...MELEE_DPS_JOBS, ...RANGED_DPS_JOBS, ...MAGIC_DPS_JOBS] as const;
export const ALL_JOBS = [...DOH_JOBS, ...DOL_JOBS, ...COMBAT_JOBS] as const;

export type CombatRole = "Tank" | "Healer" | "MeleeDPS" | "RangedDPS" | "MagicDPS";

const DISPLAY_NAMES: Record<string, string> = {
  CRP: "Carpenter", BSM: "Blacksmith", ARM: "Armorer", GSM: "Goldsmith",
  LTW: "Leatherworker", WVR: "Weaver", ALC: "Alchemist", CUL: "Culinarian",
  MIN: "Miner", BTN: "Botanist", FSH: "Fisher",
  PLD: "Paladin", WAR: "Warrior", DRK: "Dark Knight", GNB: "Gunbreaker",
  WHM: "White Mage", SCH: "Scholar", AST: "Astrologian", SGE: "Sage",
  MNK: "Monk", DRG: "Dragoon", NIN: "Ninja", SAM: "Samurai", RPR: "Reaper", VPR: "Viper",
  BRD: "Bard", MCH: "Machinist", DNC: "Dancer",
  BLM: "Black Mage", SMN: "Summoner", RDM: "Red Mage", PCT: "Pictomancer",
};

const NO_OFFHAND_JOBS = new Set(["CUL", "FSH"]);

const has = (list: readonly string[], job: string) => list.some((j) => j.toUpperCase() === job.toUpperCase());

export const isDoH = (job: string) => has(DOH_JOBS, job);
export const isDoL = (job: string) => has(DOL_JOBS, job);
export const isCombat = (job: string) => has(COMBAT_JOBS, job);
export const isKnownJob = (job: string) => job.toUpperCase() in DISPLAY_NAMES;
export const hasOffhand = (job: string) => !NO_OFFHAND_JOBS.has(job.toUpperCase());
export const jobDisplayName = (job: string) => DISPLAY_NAMES[job.toUpperCase()] ?? job;

export function combatRole(job: string): CombatRole | null {
  if (has(TANK_JOBS, job)) return "Tank";
  if (has(HEALER_JOBS, job)) return "Healer";
  if (has(MELEE_DPS_JOBS, job)) return "MeleeDPS";
  if (has(RANGED_DPS_JOBS, job)) return "RangedDPS";
  if (has(MAGIC_DPS_JOBS, job)) return "MagicDPS";
  return null;
}
