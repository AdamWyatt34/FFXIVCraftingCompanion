import type { CraftType, ItemCategory } from "@/types/api";

export const APP_NAME = "FFXIV Crafting Companion";

export const CRAFT_TYPE_INFO: Record<
  CraftType,
  { name: string; fullName: string; colorClass: string }
> = {
  CRP: { name: "CRP", fullName: "Carpenter", colorClass: "text-craft-crp" },
  BSM: { name: "BSM", fullName: "Blacksmith", colorClass: "text-craft-bsm" },
  ARM: { name: "ARM", fullName: "Armorer", colorClass: "text-craft-arm" },
  GSM: { name: "GSM", fullName: "Goldsmith", colorClass: "text-craft-gsm" },
  LTW: { name: "LTW", fullName: "Leatherworker", colorClass: "text-craft-ltw" },
  WVR: { name: "WVR", fullName: "Weaver", colorClass: "text-craft-wvr" },
  ALC: { name: "ALC", fullName: "Alchemist", colorClass: "text-craft-alc" },
  CUL: { name: "CUL", fullName: "Culinarian", colorClass: "text-craft-cul" },
};

export const ITEM_CATEGORY_INFO: Record<
  ItemCategory,
  { label: string; colorClass: string }
> = {
  Material: { label: "Material", colorClass: "text-rarity-common" },
  Craftable: { label: "Craftable", colorClass: "text-rarity-uncommon" },
  Gatherable: { label: "Gatherable", colorClass: "text-rarity-rare" },
  Vendor: { label: "Vendor", colorClass: "text-rarity-common" },
  Drop: { label: "Drop", colorClass: "text-rarity-relic" },
  Scrip: { label: "Scrip", colorClass: "text-rarity-aetherial" },
  Other: { label: "Other", colorClass: "text-text-secondary" },
};

export const SOURCE_TYPE_INFO: Record<
  string,
  { label: string; bgClass: string; textClass: string }
> = {
  Gathered: {
    label: "Gathered",
    bgClass: "bg-emerald-500/20",
    textClass: "text-emerald-400",
  },
  Vendor: {
    label: "Vendor",
    bgClass: "bg-amber-500/20",
    textClass: "text-amber-400",
  },
  Crafted: {
    label: "Crafted",
    bgClass: "bg-blue-500/20",
    textClass: "text-blue-400",
  },
  "Scrip Exchange": {
    label: "Scrip",
    bgClass: "bg-violet-500/20",
    textClass: "text-violet-400",
  },
  "Mob Drop": {
    label: "Mob Drop",
    bgClass: "bg-red-500/20",
    textClass: "text-red-400",
  },
  "Quest Reward": {
    label: "Quest",
    bgClass: "bg-cyan-500/20",
    textClass: "text-cyan-400",
  },
  "Dungeon Drop": {
    label: "Dungeon",
    bgClass: "bg-orange-500/20",
    textClass: "text-orange-400",
  },
  Desynthesis: {
    label: "Desynth",
    bgClass: "bg-pink-500/20",
    textClass: "text-pink-400",
  },
  "Aetherial Reduction": {
    label: "Reduction",
    bgClass: "bg-teal-500/20",
    textClass: "text-teal-400",
  },
  Other: {
    label: "Other",
    bgClass: "bg-gray-500/20",
    textClass: "text-gray-400",
  },
};

export const DEFAULT_PAGE_SIZE = 20;
export const MAX_PAGE_SIZE = 100;
export const MIN_SEARCH_LENGTH = 2;

export const SIMULATOR_CONSTANTS = {
  MAX_INNER_QUIET_STACKS: 10,
  MAX_ACTIONS_PER_CRAFT: 100,
  UNDO_HISTORY_LIMIT: 100,
  DEBOUNCE_STATS_MS: 500,
  CONDITION_TRANSITION_AFTER_EXCELLENT: "Poor",
  CONDITION_TRANSITION_AFTER_GOOD: "Normal",
  CONDITION_TRANSITION_AFTER_POOR: "Normal",
} as const;
