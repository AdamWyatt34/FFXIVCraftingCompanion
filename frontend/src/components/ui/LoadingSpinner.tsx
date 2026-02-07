export function LoadingSpinner({ size = 24 }: { size?: number }) {
  return (
    <div
      className="animate-spin rounded-full border-2 border-text-muted border-t-gold"
      style={{ width: size, height: size }}
      role="status"
      aria-label="Loading"
    />
  );
}
