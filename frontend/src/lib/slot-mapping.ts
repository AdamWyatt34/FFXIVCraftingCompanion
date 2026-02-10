import type { GearSlotDto } from "@/types/gear";

/**
 * Maps GearSlotDto slot data to profile slot keys.
 * Handles the Ring1/Ring2 discrepancy between gear recommendations ("Ring")
 * and profile gear ("Ring1"/"Ring2").
 */
export function gearSlotToProfileKey(
  slotKey: string,
  slotName: string,
): string {
  if (slotKey !== "Ring") return slotKey;
  if (slotName === "Ring 1") return "Ring1";
  if (slotName === "Ring 2") return "Ring2";
  return slotKey;
}

/**
 * Build a Record<string, number> suitable for PUT /api/profile/gear
 * from GearSetDto slots.
 */
export function gearSetToProfileSlots(
  slots: GearSlotDto[],
): Record<string, number> {
  const result: Record<string, number> = {};
  for (const slot of slots) {
    if (!slot.recommendation) continue;
    const profileKey = gearSlotToProfileKey(slot.slotKey, slot.slotName);
    result[profileKey] = slot.recommendation.equipmentId;
  }
  return result;
}
