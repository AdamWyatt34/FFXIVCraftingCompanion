export function ShoppingListSkeleton() {
  return (
    <div className="space-y-4 animate-pulse">
      {[1, 2, 3].map((section) => (
        <div
          key={section}
          className="bg-bg-surface rounded-lg border border-border overflow-hidden"
        >
          <div className="flex items-center gap-2 px-4 py-3 bg-bg-elevated/50 border-b border-border">
            <div className="w-4 h-4 bg-bg-elevated rounded" />
            <div className="h-4 w-32 bg-bg-elevated rounded" />
            <div className="h-3 w-8 bg-bg-elevated rounded" />
          </div>
          <div className="divide-y divide-border/50">
            {Array.from({ length: section === 1 ? 2 : 4 }, (_, i) => (
              <div key={i} className="flex items-center gap-3 px-4 py-2.5">
                <div className="w-8 h-8 bg-bg-elevated rounded" />
                <div className="flex-1 space-y-1">
                  <div className="h-4 w-36 bg-bg-elevated rounded" />
                  <div className="h-3 w-48 bg-bg-elevated rounded" />
                </div>
                <div className="h-5 w-16 bg-bg-elevated rounded-full" />
                <div className="h-4 w-10 bg-bg-elevated rounded" />
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

export function CraftingStepsSkeleton() {
  return (
    <div className="space-y-3 animate-pulse">
      {Array.from({ length: 4 }, (_, i) => (
        <div key={i} className="flex gap-4">
          <div className="flex flex-col items-center">
            <div className="w-10 h-10 bg-bg-elevated rounded-full" />
            {i < 3 && <div className="w-0.5 flex-1 bg-border mt-2" />}
          </div>
          <div className="flex-1 bg-bg-surface rounded-lg border border-border p-4 space-y-2">
            <div className="flex items-center gap-3">
              <div className="w-8 h-8 bg-bg-elevated rounded" />
              <div className="h-4 w-40 bg-bg-elevated rounded" />
              <div className="h-5 w-14 bg-bg-elevated rounded-full ml-auto" />
            </div>
            <div className="flex items-center gap-2">
              <div className="h-3 w-20 bg-bg-elevated rounded" />
              <div className="h-3 w-16 bg-bg-elevated rounded" />
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
