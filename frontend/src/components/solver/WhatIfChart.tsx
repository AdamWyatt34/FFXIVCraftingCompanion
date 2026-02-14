"use client";

import { memo } from "react";
import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  ReferenceDot,
} from "recharts";

interface WhatIfChartProps {
  data: Array<{ statValue: number; hqPercent: number; steps: number }>;
  statLabel: string;
  currentStatValue?: number;
  currentHqPercent?: number;
}

function WhatIfChartInner({
  data,
  statLabel,
  currentStatValue,
  currentHqPercent,
}: WhatIfChartProps) {
  if (data.length < 2) {
    return (
      <div className="flex items-center justify-center h-[250px] text-text-muted text-sm">
        Adjust a stat slider to see how it affects HQ%
      </div>
    );
  }

  const trendDescription =
    data.length >= 2
      ? `HQ% ${data[data.length - 1].hqPercent >= data[0].hqPercent ? "increases" : "decreases"} from ${data[0].hqPercent}% to ${data[data.length - 1].hqPercent}% as ${statLabel} goes from ${data[0].statValue} to ${data[data.length - 1].statValue}`
      : `HQ% chart for ${statLabel}`;

  return (
    <div
      role="img"
      aria-label={trendDescription}
    >
      <ResponsiveContainer width="100%" height={250}>
        <LineChart data={data} margin={{ top: 10, right: 20, bottom: 20, left: 10 }}>
          <XAxis
            dataKey="statValue"
            stroke="#6b6b80"
            fontSize={12}
            label={{
              value: statLabel,
              position: "insideBottom",
              offset: -10,
              fill: "#a0a0b8",
              fontSize: 12,
            }}
          />
          <YAxis
            domain={[0, 100]}
            stroke="#6b6b80"
            fontSize={12}
            label={{
              value: "HQ %",
              angle: -90,
              position: "insideLeft",
              offset: 5,
              fill: "#a0a0b8",
              fontSize: 12,
            }}
          />
          <Tooltip
            contentStyle={{
              backgroundColor: "#202040",
              border: "1px solid #3a3a5a",
              borderRadius: "8px",
              color: "#e8e8f0",
              fontSize: 12,
            }}
            labelFormatter={(val) => `${statLabel}: ${val}`}
            formatter={(value, name) => {
              if (name === "hqPercent") return [`${value}%`, "HQ"];
              return [String(value), String(name)];
            }}
          />
          <Line
            type="monotone"
            dataKey="hqPercent"
            stroke="#e6b422"
            strokeWidth={2}
            dot={{ fill: "#e6b422", r: 4 }}
            activeDot={{ fill: "#f0cc5b", r: 6 }}
          />
          {currentStatValue !== undefined && currentHqPercent !== undefined && (
            <ReferenceDot
              x={currentStatValue}
              y={currentHqPercent}
              r={8}
              fill="#e6b422"
              stroke="#f0cc5b"
              strokeWidth={2}
            />
          )}
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}

export const WhatIfChart = memo(WhatIfChartInner);
