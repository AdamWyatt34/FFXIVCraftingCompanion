function Bone({ className }: { className?: string }) {
  return <div className={`bg-bg-elevated animate-pulse rounded ${className ?? ""}`} />;
}

export function JourneySkeleton() {
  return (
    <div className="p-4 space-y-6 max-w-4xl mx-auto">
      {/* Header skeleton */}
      <div className="flex items-start gap-4">
        <Bone className="w-16 h-16 rounded" />
        <div className="flex-1 space-y-2">
          <Bone className="h-6 w-48" />
          <Bone className="h-4 w-32" />
        </div>
      </div>

      {/* Stat check skeleton */}
      <Bone className="h-32 rounded-lg" />

      {/* Materials skeleton */}
      <div className="space-y-2">
        <Bone className="h-10 rounded-lg" />
        <Bone className="h-12 rounded-lg" />
        <Bone className="h-12 rounded-lg" />
        <Bone className="h-12 rounded-lg" />
      </div>

      {/* Cost estimate skeleton */}
      <Bone className="h-12 rounded-lg" />

      {/* Gathering guide skeleton */}
      <Bone className="h-12 rounded-lg" />
    </div>
  );
}
