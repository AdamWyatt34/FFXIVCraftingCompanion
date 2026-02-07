import { AlertTriangle } from "lucide-react";

interface ErrorMessageProps {
  message: string;
  onRetry?: () => void;
}

export function ErrorMessage({ message, onRetry }: ErrorMessageProps) {
  return (
    <div className="flex flex-col items-center gap-3 p-6 text-center">
      <AlertTriangle className="text-red-400" size={32} />
      <p className="text-text-secondary">{message}</p>
      {onRetry && (
        <button
          onClick={onRetry}
          className="px-4 py-2 bg-bg-elevated text-gold rounded-lg hover:bg-bg-tertiary transition-colors"
        >
          Try Again
        </button>
      )}
    </div>
  );
}
