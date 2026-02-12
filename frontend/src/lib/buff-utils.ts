import type { ActiveBuff, BuffChange } from "@/types/simulator";

/**
 * Compute the diff between two buff arrays to show what changed in a step.
 * For step 1, pass empty array / 0 as "before" values.
 */
export function diffBuffs(
  before: ActiveBuff[],
  after: ActiveBuff[],
  iqBefore: number,
  iqAfter: number,
): BuffChange[] {
  const changes: BuffChange[] = [];
  const beforeMap = new Map(before.map((b) => [b.name, b]));
  const afterMap = new Map(after.map((b) => [b.name, b]));

  // IQ stack changes (tracked separately from buff list)
  if (iqBefore !== iqAfter) {
    changes.push({
      name: "Inner Quiet",
      type: "changed",
      stacksBefore: iqBefore,
      stacksAfter: iqAfter,
    });
  }

  for (const [name, aBuff] of afterMap) {
    if (name === "Inner Quiet") continue; // handled above
    const bBuff = beforeMap.get(name);
    if (!bBuff) {
      // New buff added
      changes.push({ name, type: "added", turnsAfter: aBuff.remainingTurns });
    } else if (aBuff.remainingTurns > bBuff.remainingTurns) {
      // Buff was refreshed (duration increased)
      changes.push({ name, type: "added", turnsAfter: aBuff.remainingTurns });
    }
  }

  for (const [name] of beforeMap) {
    if (name === "Inner Quiet") continue;
    if (!afterMap.has(name)) {
      changes.push({ name, type: "removed" });
    }
  }

  return changes;
}
