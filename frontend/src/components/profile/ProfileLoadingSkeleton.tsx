export function ProfileLoadingSkeleton() {
  return (
    <div className="flex-1 overflow-y-auto p-4 animate-pulse">
      <div className="space-y-6 max-w-4xl mx-auto">
        {/* Header skeleton */}
        <div className="flex items-center gap-4">
          <div className="w-16 h-16 rounded-full bg-bg-elevated shrink-0" />
          <div className="space-y-2 flex-1">
            <div className="h-5 w-40 bg-bg-elevated rounded" />
            <div className="h-4 w-28 bg-bg-elevated rounded" />
          </div>
        </div>

        {/* Stats skeleton */}
        <div className="p-4 bg-bg-surface rounded-lg border border-border space-y-3">
          <div className="h-4 w-24 bg-bg-elevated rounded" />
          <div className="grid grid-cols-3 gap-4">
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i} className="space-y-1">
                <div className="h-3 w-16 bg-bg-elevated rounded" />
                <div className="h-6 w-12 bg-bg-elevated rounded" />
              </div>
            ))}
          </div>
        </div>

        {/* Job levels skeleton */}
        <div className="p-4 bg-bg-surface rounded-lg border border-border space-y-3">
          <div className="h-4 w-32 bg-bg-elevated rounded" />
          <div className="grid grid-cols-4 sm:grid-cols-8 gap-3">
            {Array.from({ length: 8 }).map((_, i) => (
              <div key={i} className="flex flex-col items-center gap-1">
                <div className="w-10 h-10 rounded bg-bg-elevated" />
                <div className="h-3 w-6 bg-bg-elevated rounded" />
              </div>
            ))}
          </div>
        </div>

        {/* Saved lists skeleton */}
        <div className="space-y-3">
          <div className="h-4 w-24 bg-bg-elevated rounded" />
          <div className="flex gap-2">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="h-8 w-24 bg-bg-elevated rounded-full" />
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
