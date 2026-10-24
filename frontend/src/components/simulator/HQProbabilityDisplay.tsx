"use client";

import { Sparkles } from "lucide-react";

function getHQColor(pct: number): string {
  if (pct >= 100) return "#e6b422";
  if (pct >= 90) return "#22c55e";
  if (pct >= 61) return "#4ade80";
  if (pct >= 31) return "#f59e0b";
  return "#ef4444";
}

const RADIUS = 50;
const CIRCUMFERENCE = 2 * Math.PI * RADIUS;

interface HQProbabilityDisplayProps {
  hqPercent: number;
  compact?: boolean;
}

export function HQProbabilityDisplay({ hqPercent, compact }: HQProbabilityDisplayProps) {
  const color = getHQColor(hqPercent);
  const offset = CIRCUMFERENCE - (hqPercent / 100) * CIRCUMFERENCE;

  const containerSize = compact ? "w-[70px] h-[70px] sm:w-[100px] sm:h-[100px]" : "w-[100px] h-[100px]";
  const textClass = compact ? "text-lg sm:text-2xl" : "text-2xl";
  const subTextClass = compact ? "text-[8px] sm:text-[10px]" : "text-[10px]";

  return (
    <span title="Based on quality percentage. 100% quality = 100% HQ chance.">
      <div className={`relative ${containerSize} shrink-0`}>
        <svg viewBox="0 0 120 120" className="w-full h-full">
          {/* Background circle */}
          <circle
            cx="60"
            cy="60"
            r={RADIUS}
            fill="none"
            stroke="#2a2a4a"
            strokeWidth="8"
          />
          {/* Foreground arc */}
          <circle
            cx="60"
            cy="60"
            r={RADIUS}
            fill="none"
            stroke={color}
            strokeWidth="8"
            strokeLinecap="round"
            strokeDasharray={CIRCUMFERENCE}
            strokeDashoffset={offset}
            transform="rotate(-90 60 60)"
            style={{
              transition:
                "stroke-dashoffset 0.5s ease-out, stroke 0.3s ease",
            }}
          />
          {/* Percentage text */}
          <text
            x="60"
            y="56"
            textAnchor="middle"
            dominantBaseline="middle"
            className={`${textClass} font-bold`}
            fill={color}
            style={{ transition: "fill 0.3s ease" }}
          >
            {hqPercent}%
          </text>
          <text
            x="60"
            y="76"
            textAnchor="middle"
            dominantBaseline="middle"
            className={subTextClass}
            fill="#6b6b80"
          >
            chance of HQ
          </text>
        </svg>
        {hqPercent === 100 && (
          <span className="absolute top-0 right-0 animate-pulse-glow">
            <Sparkles size={16} className="text-gold" />
          </span>
        )}
      </div>
    </span>
  );
}
