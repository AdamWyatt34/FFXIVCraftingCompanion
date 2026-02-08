const SECTIONS = [
  { label: "Weapons", count: 2 },
  { label: "Armor", count: 5 },
  { label: "Accessories", count: 5 },
] as const;

function SlotSkeleton() {
  return (
    <div className="bg-bg-surface rounded-lg border border-border p-3 space-y-2">
      <div className="h-3 w-20 bg-bg-elevated rounded" />
      <div className="flex items-center gap-3">
        <div className="w-10 h-10 bg-bg-elevated rounded shrink-0" />
        <div className="flex-1 space-y-1.5">
          <div className="h-4 w-3/4 bg-bg-elevated rounded" />
          <div className="h-3 w-1/2 bg-bg-elevated rounded" />
        </div>
      </div>
      <div className="flex gap-3">
        <div className="h-3 w-14 bg-bg-elevated rounded" />
        <div className="h-3 w-14 bg-bg-elevated rounded" />
        <div className="h-3 w-10 bg-bg-elevated rounded" />
      </div>
    </div>
  );
}

export function GearLoadingSkeleton() {
  return (
    <div className="space-y-6 animate-pulse">
      {/* Stats summary skeleton */}
      <div className="bg-bg-surface rounded-lg border border-gold/30 p-4 space-y-3">
        <div className="flex items-center justify-between">
          <div className="h-4 w-24 bg-bg-elevated rounded" />
          <div className="h-5 w-16 bg-bg-elevated rounded-full" />
        </div>
        <div className="grid grid-cols-3 gap-3">
          {[1, 2, 3].map((i) => (
            <div key={i} className="text-center space-y-1">
              <div className="h-6 w-16 bg-bg-elevated rounded mx-auto" />
              <div className="h-3 w-20 bg-bg-elevated rounded mx-auto" />
            </div>
          ))}
        </div>
      </div>

      {/* Section skeletons */}
      {SECTIONS.map((section) => (
        <div key={section.label} className="space-y-3">
          <div className="flex items-center gap-3">
            <span className="h-px flex-1 bg-border" />
            <div className="h-3 w-20 bg-bg-elevated rounded" />
            <span className="h-px flex-1 bg-border" />
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            {Array.from({ length: section.count }, (_, i) => (
              <SlotSkeleton key={i} />
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}
