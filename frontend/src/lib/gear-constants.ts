export type GearJob =
  | "CRP" | "BSM" | "ARM" | "GSM" | "LTW" | "WVR" | "ALC" | "CUL"
  | "MIN" | "BTN" | "FSH";

export type JobCategory = "DoH" | "DoL";

export interface JobInfo {
  abbreviation: GearJob;
  fullName: string;
  category: JobCategory;
  classJobId: number;
  colorClass: string;
  hasOffhand: boolean;
}

export const DOH_JOBS: GearJob[] = ["CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL"];
export const DOL_JOBS: GearJob[] = ["MIN", "BTN", "FSH"];

export const JOB_INFO: Record<GearJob, JobInfo> = {
  CRP: { abbreviation: "CRP", fullName: "Carpenter",     category: "DoH", classJobId: 8,  colorClass: "text-craft-crp", hasOffhand: true },
  BSM: { abbreviation: "BSM", fullName: "Blacksmith",    category: "DoH", classJobId: 9,  colorClass: "text-craft-bsm", hasOffhand: true },
  ARM: { abbreviation: "ARM", fullName: "Armorer",       category: "DoH", classJobId: 10, colorClass: "text-craft-arm", hasOffhand: true },
  GSM: { abbreviation: "GSM", fullName: "Goldsmith",     category: "DoH", classJobId: 11, colorClass: "text-craft-gsm", hasOffhand: true },
  LTW: { abbreviation: "LTW", fullName: "Leatherworker", category: "DoH", classJobId: 12, colorClass: "text-craft-ltw", hasOffhand: true },
  WVR: { abbreviation: "WVR", fullName: "Weaver",        category: "DoH", classJobId: 13, colorClass: "text-craft-wvr", hasOffhand: true },
  ALC: { abbreviation: "ALC", fullName: "Alchemist",     category: "DoH", classJobId: 14, colorClass: "text-craft-alc", hasOffhand: true },
  CUL: { abbreviation: "CUL", fullName: "Culinarian",    category: "DoH", classJobId: 15, colorClass: "text-craft-cul", hasOffhand: false },
  MIN: { abbreviation: "MIN", fullName: "Miner",         category: "DoL", classJobId: 16, colorClass: "text-emerald-400", hasOffhand: true },
  BTN: { abbreviation: "BTN", fullName: "Botanist",      category: "DoL", classJobId: 17, colorClass: "text-emerald-400", hasOffhand: true },
  FSH: { abbreviation: "FSH", fullName: "Fisher",        category: "DoL", classJobId: 18, colorClass: "text-blue-400",    hasOffhand: false },
};

export const LEVEL_PRESETS = [50, 60, 70, 80, 90, 100] as const;

export const PROFILE_DISPLAY_NAMES: Record<string, string> = {
  Balanced: "Balanced",
  ProgressFocused: "Progress",
  QualityFocused: "Quality",
  CpFocused: "CP Focus",
  GatheringBalanced: "Balanced",
  PerceptionFocused: "Perception",
};

export const RARITY_COLOR_CLASS: Record<string, string> = {
  Common: "text-rarity-common",
  Uncommon: "text-rarity-uncommon",
  Rare: "text-rarity-rare",
  Relic: "text-rarity-relic",
};

export function getJobIconUrl(classJobId: number): string {
  const padded = String(classJobId).padStart(2, "0");
  return `https://v2.xivapi.com/api/asset/ui/icon/062000/0620${padded}_hr1.tex?format=png`;
}

// Slot grouping for GearGrid layout sections
export type SlotGroup = "Weapons" | "Armor" | "Accessories";

export const SLOT_GROUPS: Record<SlotGroup, string[]> = {
  Weapons: ["MainHand", "OffHand"],
  Armor: ["Head", "Body", "Hands", "Legs", "Feet"],
  Accessories: ["Earring", "Necklace", "Bracelet", "Ring"],
};

const ACCESSORY_KEYS = new Set(["Earring", "Necklace", "Bracelet", "Ring"]);

export function getSlotGroup(slotKey: string): SlotGroup {
  if (slotKey === "MainHand" || slotKey === "OffHand") return "Weapons";
  if (ACCESSORY_KEYS.has(slotKey)) return "Accessories";
  return "Armor";
}
