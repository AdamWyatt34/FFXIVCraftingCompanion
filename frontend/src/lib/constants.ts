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

export const DEFAULT_PAGE_SIZE = 20;
export const MAX_PAGE_SIZE = 100;
export const MIN_SEARCH_LENGTH = 2;
