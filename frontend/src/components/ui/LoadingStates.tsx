import { AlertCircle, SearchX } from "lucide-react";

export function LoadingSkeleton({ count = 6 }: { count?: number }) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 p-4">
      {Array.from({ length: count }, (_, i) => (
        <div
          key={i}
          className="flex items-center gap-3 p-3 rounded-lg bg-bg-surface animate-pulse"
        >
          <div className="w-8 h-8 rounded bg-bg-elevated shrink-0" />
          <div className="flex-1 space-y-2">
            <div className="h-4 bg-bg-elevated rounded w-3/4" />
            <div className="h-3 bg-bg-elevated rounded w-1/2" />
          </div>
        </div>
      ))}
    </div>
  );
}

interface ErrorStateProps {
  message: string;
  onRetry?: () => void;
}

export function ErrorState({ message, onRetry }: ErrorStateProps) {
  return (
    <div className="flex flex-col items-center gap-4 p-8 text-center">
      <AlertCircle className="text-red-400" size={40} />
      <div>
        <p className="text-text-primary font-medium">Something went wrong</p>
        <p className="text-text-secondary text-sm mt-1">{message}</p>
      </div>
      {onRetry && (
        <button
          onClick={onRetry}
          className="px-5 py-2 bg-bg-elevated text-gold rounded-lg hover:bg-bg-tertiary transition-colors font-medium"
        >
          Try Again
        </button>
      )}
    </div>
  );
}

interface EmptyStateProps {
  query: string;
}

export function EmptyState({ query }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center gap-4 p-8 text-center">
      <SearchX className="text-text-muted" size={40} />
      <div>
        <p className="text-text-primary font-medium">No results found</p>
        <p className="text-text-secondary text-sm mt-1">
          No items match &quot;{query}&quot;. Try a different search term.
        </p>
      </div>
      <div className="text-text-muted text-xs space-y-1">
        <p>Suggestions:</p>
        <ul className="list-disc list-inside text-left">
          <li>Check for typos</li>
          <li>Try a shorter or more general term</li>
          <li>Search by item name (e.g., &quot;Ironworks&quot;, &quot;Rarefied&quot;)</li>
        </ul>
      </div>
    </div>
  );
}
