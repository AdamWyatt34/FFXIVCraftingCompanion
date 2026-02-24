"use client";

import { useState, useEffect } from "react";
import {
  isWindowActive,
  timeUntilWindowSeconds,
  formatCountdown,
} from "@/lib/eorzean-time";

interface TimedNodeCountdownProps {
  startHour: number;
  endHour: number;
}

export function TimedNodeCountdown({ startHour, endHour }: TimedNodeCountdownProps) {
  const [secondsUntil, setSecondsUntil] = useState(() =>
    timeUntilWindowSeconds(startHour, endHour),
  );
  const [active, setActive] = useState(() =>
    isWindowActive(startHour, endHour),
  );

  useEffect(() => {
    const update = () => {
      const a = isWindowActive(startHour, endHour);
      setActive(a);
      setSecondsUntil(timeUntilWindowSeconds(startHour, endHour));
    };

    // Tick every second when close (<5 real-min), otherwise every 10s
    const interval = secondsUntil > 0 && secondsUntil < 300 ? 1000 : 10_000;
    const id = setInterval(update, interval);
    return () => clearInterval(id);
  }, [startHour, endHour, secondsUntil]);

  const timeLabel = `${String(startHour).padStart(2, "0")}:00–${String(endHour).padStart(2, "0")}:00`;

  if (active) {
    return (
      <span className="inline-flex items-center gap-1 text-xs">
        <span className="text-emerald-400 font-semibold">ACTIVE</span>
        <span className="text-text-muted font-mono">{timeLabel}</span>
      </span>
    );
  }

  if (secondsUntil < 300) {
    return (
      <span className="inline-flex items-center gap-1 text-xs">
        <span className="text-amber-400 font-semibold">{formatCountdown(secondsUntil)}</span>
        <span className="text-text-muted font-mono">{timeLabel}</span>
      </span>
    );
  }

  return (
    <span className="inline-flex items-center gap-1 text-xs">
      <span className="text-text-muted">~{formatCountdown(secondsUntil)}</span>
      <span className="text-text-muted font-mono">{timeLabel}</span>
    </span>
  );
}
