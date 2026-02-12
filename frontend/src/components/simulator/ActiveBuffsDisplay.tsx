"use client";

import { useRef, useEffect, useState } from "react";
import { BUFF_DISPLAY } from "@/lib/action-icons";
import type { ActiveBuff } from "@/types/simulator";

const SPECIAL_BUFFS = new Set(["Heart and Soul", "Trained Perfection"]);

interface ActiveBuffsDisplayProps {
  buffs: ActiveBuff[];
  innerQuietStacks: number;
}

export function ActiveBuffsDisplay({
  buffs,
  innerQuietStacks,
}: ActiveBuffsDisplayProps) {
  const prevBuffsRef = useRef<ActiveBuff[]>([]);
  const [exitingBuffs, setExitingBuffs] = useState<Set<string>>(new Set());

  // Detect removed buffs for exit animation
  useEffect(() => {
    const prevNames = new Set(prevBuffsRef.current.map((b) => b.name));
    const currentNames = new Set(buffs.map((b) => b.name));
    const removed = new Set<string>();
    for (const name of prevNames) {
      if (!currentNames.has(name)) removed.add(name);
    }
    if (removed.size > 0) {
      setExitingBuffs(removed);
      const timer = setTimeout(() => setExitingBuffs(new Set()), 150);
      return () => clearTimeout(timer);
    }
    prevBuffsRef.current = buffs;
  }, [buffs]);

  if (buffs.length === 0 && innerQuietStacks === 0) return null;

  // Build aria description for screen readers
  const buffDescriptions = buffs.map((buff) => {
    const display = BUFF_DISPLAY[buff.name];
    const label = display?.label ?? buff.name;
    if (buff.name === "Inner Quiet") return `${label}: ${innerQuietStacks} stacks`;
    if (SPECIAL_BUFFS.has(buff.name)) return `${label}: Active`;
    return `${label}: ${buff.remainingTurns} turns`;
  });

  return (
    <div
      className="flex flex-nowrap overflow-x-auto sm:flex-wrap sm:overflow-visible gap-1.5"
      style={{ scrollbarWidth: "none" }}
      role="status"
      aria-live="polite"
      aria-label={`Active buffs: ${buffDescriptions.join(", ") || "None"}`}
    >
      {buffs.map((buff) => {
        const display = BUFF_DISPLAY[buff.name] ?? {
          label: buff.name,
          color: "#9ca3af",
          abbreviation: buff.name.slice(0, 3),
        };

        const isInnerQuiet = buff.name === "Inner Quiet";
        const isSpecial = SPECIAL_BUFFS.has(buff.name);
        const isExpiring =
          !isInnerQuiet && !isSpecial && buff.remainingTurns === 1;

        let valueDisplay: string;
        if (isInnerQuiet) {
          valueDisplay = `\u00d7${innerQuietStacks}`;
        } else if (isSpecial) {
          valueDisplay = "\u2713";
        } else {
          valueDisplay = String(buff.remainingTurns);
        }

        const tooltipText = isInnerQuiet
          ? `${display.label}: ${innerQuietStacks} stacks`
          : isSpecial
            ? `${display.label}: Active`
            : `${display.label}: ${buff.remainingTurns} turn${buff.remainingTurns !== 1 ? "s" : ""} remaining`;

        return (
          <span key={buff.name} title={tooltipText} className="shrink-0 sm:shrink">
            <div
              className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium animate-buff-enter ${
                isExpiring ? "animate-pulse-glow" : ""
              }`}
              style={{
                backgroundColor: `${display.color}20`,
                color: display.color,
                borderColor: `${display.color}40`,
                borderWidth: 1,
                borderStyle: "solid",
              }}
            >
              <span>{display.abbreviation}</span>
              <span className="opacity-70">{valueDisplay}</span>
            </div>
          </span>
        );
      })}
      {/* Exiting buffs (brief animation) */}
      {Array.from(exitingBuffs).map((name) => {
        const display = BUFF_DISPLAY[name] ?? {
          label: name,
          color: "#9ca3af",
          abbreviation: name.slice(0, 3),
        };
        return (
          <span key={`exit-${name}`} className="shrink-0 sm:shrink">
            <div
              className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium animate-buff-exit"
              style={{
                backgroundColor: `${display.color}20`,
                color: display.color,
                borderColor: `${display.color}40`,
                borderWidth: 1,
                borderStyle: "solid",
              }}
            >
              <span>{display.abbreviation}</span>
            </div>
          </span>
        );
      })}
    </div>
  );
}
