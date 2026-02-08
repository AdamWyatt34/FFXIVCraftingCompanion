import { SOURCE_TYPE_INFO } from "@/lib/constants";

interface SourceBadgeProps {
  sourceType: string;
  className?: string;
}

export function SourceBadge({ sourceType, className = "" }: SourceBadgeProps) {
  const info = SOURCE_TYPE_INFO[sourceType] ?? SOURCE_TYPE_INFO.Other;

  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${info.bgClass} ${info.textClass} ${className}`}
    >
      {info.label}
    </span>
  );
}
