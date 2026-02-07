"use client";

import Image from "next/image";
import { useState } from "react";
import { HelpCircle } from "lucide-react";

const sizes = {
  sm: 24,
  md: 32,
  lg: 40,
  xl: 48,
} as const;

type IconSize = keyof typeof sizes;

interface ItemIconProps {
  iconUrl: string;
  alt: string;
  size?: IconSize;
  className?: string;
}

export function ItemIcon({
  iconUrl,
  alt,
  size = "md",
  className = "",
}: ItemIconProps) {
  const [hasError, setHasError] = useState(false);
  const px = sizes[size];

  if (!iconUrl || hasError) {
    return (
      <div
        className={`flex items-center justify-center bg-bg-elevated rounded shrink-0 ${className}`}
        style={{ width: px, height: px }}
      >
        <HelpCircle size={px * 0.6} className="text-text-muted" />
      </div>
    );
  }

  return (
    <Image
      src={iconUrl}
      alt={alt}
      width={px}
      height={px}
      className={`rounded shrink-0 ${className}`}
      onError={() => setHasError(true)}
      unoptimized
    />
  );
}
