"use client";

import { useState, useEffect, useRef, useCallback } from "react";
import { createPortal } from "react-dom";
import { X, Copy, Check, ChevronDown, ChevronUp } from "lucide-react";
import { exportCraftingMacro, ApiClientError } from "@/lib/api-client";
import { formatRotationSummary } from "@/lib/format-utils";
import type {
  MacroOptions,
  ExportMacroResult,
  StepResult,
  CraftingState,
} from "@/types/simulator";

interface MacroExportPanelProps {
  isOpen: boolean;
  actionIds: number[];
  stateHistory: StepResult[];
  currentState: CraftingState | null;
  onClose: () => void;
}

// ── Syntax-highlighted macro line ──

function MacroLine({ line }: { line: string }) {
  // /ac "ActionName" <wait.N>
  const acMatch = line.match(/^(\/ac)\s+"([^"]+)"\s+(<wait\.\d+>)$/);
  if (acMatch) {
    return (
      <div>
        <span className="text-blue-400">{acMatch[1]}</span>
        <span className="text-text-primary"> &quot;</span>
        <span className="text-gold">{acMatch[2]}</span>
        <span className="text-text-primary">&quot; </span>
        <span className="text-text-muted">{acMatch[3]}</span>
      </div>
    );
  }

  // /echo ... <se.N>
  if (line.startsWith("/echo")) {
    return <div className="text-emerald-400">{line}</div>;
  }

  return <div className="text-text-secondary">{line}</div>;
}

// ── Copy button with feedback ──

function CopyButton({
  text,
  label,
}: {
  text: string;
  label: string;
}) {
  const [copied, setCopied] = useState(false);
  const timeoutRef = useRef<ReturnType<typeof setTimeout>>(undefined);

  const handleCopy = useCallback(async () => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      if (timeoutRef.current) clearTimeout(timeoutRef.current);
      timeoutRef.current = setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard API not available
    }
  }, [text]);

  useEffect(() => {
    return () => {
      if (timeoutRef.current) clearTimeout(timeoutRef.current);
    };
  }, []);

  return (
    <button
      onClick={handleCopy}
      className={`flex items-center gap-1 px-2 py-1 rounded text-xs font-medium transition-colors ${
        copied
          ? "bg-emerald-500/20 text-emerald-400"
          : "bg-bg-elevated text-text-secondary hover:text-text-primary border border-border"
      }`}
    >
      {copied ? (
        <>
          <Check size={12} />
          Copied!
        </>
      ) : (
        <>
          <Copy size={12} />
          {label}
        </>
      )}
    </button>
  );
}

// ── Options section ──

function MacroOptionsSection({
  options,
  onChange,
}: {
  options: MacroOptions;
  onChange: (opts: MacroOptions) => void;
}) {
  const [expanded, setExpanded] = useState(false);

  return (
    <div className="border-t border-border pt-3">
      <button
        onClick={() => setExpanded(!expanded)}
        className="flex items-center gap-1 text-sm text-text-secondary hover:text-text-primary transition-colors mb-2"
      >
        {expanded ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
        Options
      </button>

      {expanded && (
        <div className="space-y-3 animate-expand-down">
          {/* Lines per macro */}
          <div>
            <label className="text-xs text-text-muted block mb-1">
              Lines per macro
            </label>
            <div className="flex gap-2">
              {[12, 13, 14].map((n) => (
                <button
                  key={n}
                  onClick={() => onChange({ ...options, linesPerMacro: n })}
                  className={`px-3 py-1 rounded text-xs font-medium border transition-colors ${
                    options.linesPerMacro === n
                      ? "border-gold text-gold bg-gold/10"
                      : "border-border text-text-secondary hover:text-text-primary"
                  }`}
                >
                  {n}
                </button>
              ))}
            </div>
          </div>

          {/* Sound effect */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-xs text-text-muted block mb-1">
                Sound effect
              </label>
              <select
                value={options.soundEffect ?? 1}
                onChange={(e) =>
                  onChange({
                    ...options,
                    soundEffect: parseInt(e.target.value),
                  })
                }
                className="w-full px-2 py-1 bg-bg-elevated border border-border rounded text-sm text-text-primary"
              >
                {Array.from({ length: 16 }, (_, i) => i + 1).map((n) => (
                  <option key={n} value={n}>
                    SE.{n}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="text-xs text-text-muted block mb-1">
                Completion sound
              </label>
              <select
                value={options.completeSoundEffect ?? 2}
                onChange={(e) =>
                  onChange({
                    ...options,
                    completeSoundEffect: parseInt(e.target.value),
                  })
                }
                className="w-full px-2 py-1 bg-bg-elevated border border-border rounded text-sm text-text-primary"
              >
                {Array.from({ length: 16 }, (_, i) => i + 1).map((n) => (
                  <option key={n} value={n}>
                    SE.{n}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Include echo */}
          <label className="flex items-center gap-2 cursor-pointer">
            <input
              type="checkbox"
              checked={options.includeEcho !== false}
              onChange={(e) =>
                onChange({ ...options, includeEcho: e.target.checked })
              }
              className="w-4 h-4 rounded border-border bg-bg-elevated accent-gold"
            />
            <span className="text-xs text-text-secondary">
              Include /echo lines
            </span>
          </label>
        </div>
      )}
    </div>
  );
}

// ── Main component ──

export function MacroExportPanel({
  isOpen,
  actionIds,
  stateHistory,
  currentState,
  onClose,
}: MacroExportPanelProps) {
  const [macroResult, setMacroResult] = useState<ExportMacroResult | null>(
    null,
  );
  const [options, setOptions] = useState<MacroOptions>({
    linesPerMacro: 14,
    soundEffect: 1,
    completeSoundEffect: 2,
    includeEcho: true,
  });
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(undefined);

  // Fetch macros when panel opens or options change
  useEffect(() => {
    if (!isOpen || actionIds.length === 0) {
      setMacroResult(null);
      return;
    }

    // Debounce option changes
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(async () => {
      abortRef.current?.abort();
      const controller = new AbortController();
      abortRef.current = controller;

      setIsLoading(true);
      setError(null);

      try {
        const result = await exportCraftingMacro(
          { actionIds, options },
          controller.signal,
        );
        if (!controller.signal.aborted) {
          setMacroResult(result);
        }
      } catch (err) {
        if (err instanceof DOMException && err.name === "AbortError") return;
        if (!controller.signal.aborted) {
          setError(
            err instanceof ApiClientError
              ? err.body.error
              : "Failed to export macro",
          );
        }
      } finally {
        if (!controller.signal.aborted) setIsLoading(false);
      }
    }, 300);

    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [isOpen, actionIds, options]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      abortRef.current?.abort();
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, []);

  // Close on Escape
  useEffect(() => {
    if (!isOpen) return;
    function handleKey(e: KeyboardEvent) {
      if (e.key === "Escape") onClose();
    }
    document.addEventListener("keydown", handleKey);
    return () => document.removeEventListener("keydown", handleKey);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  // Rotation summary
  const rotationSummary =
    stateHistory.length > 0 && currentState
      ? formatRotationSummary(stateHistory, currentState)
      : null;

  const macroCount = macroResult?.macros.length ?? 0;

  const content = (
    <>
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-border">
        <h2 className="text-base font-semibold text-text-primary">
          Export as Macro
        </h2>
        <button
          onClick={onClose}
          className="text-text-muted hover:text-text-secondary transition-colors"
        >
          <X size={18} />
        </button>
      </div>

      {/* Body */}
      <div className="flex-1 overflow-y-auto px-4 py-3 space-y-4">
        {actionIds.length === 0 ? (
          <p className="text-sm text-text-muted text-center py-8">
            Execute crafting actions to generate a macro.
          </p>
        ) : (
          <>
            {/* Summary line */}
            <div className="flex items-center justify-between text-sm text-text-secondary">
              <span>
                Rotation: {actionIds.length} actions
                {macroCount > 0 && ` → ${macroCount} macro${macroCount > 1 ? "s" : ""}`}
              </span>
            </div>

            {/* Rotation summary */}
            {rotationSummary && (
              <div className="bg-bg-elevated rounded-lg p-3 border border-border">
                <div className="flex items-start justify-between gap-2">
                  <p className="text-xs text-text-secondary whitespace-pre-wrap break-words flex-1">
                    {rotationSummary}
                  </p>
                  <CopyButton text={rotationSummary} label="Copy" />
                </div>
              </div>
            )}

            {/* Loading / Error */}
            {isLoading && (
              <p className="text-sm text-text-muted text-center py-4">
                Generating macros...
              </p>
            )}
            {error && (
              <p className="text-sm text-red-400 text-center py-2">{error}</p>
            )}

            {/* Macro blocks */}
            {macroResult &&
              macroResult.macros.map((macro, i) => (
                <div key={i}>
                  {/* Macro header */}
                  {macroCount > 1 && (
                    <div className="flex items-center justify-between mb-1">
                      <span className="text-xs font-medium text-text-secondary">
                        Macro {i + 1} of {macroCount}
                      </span>
                    </div>
                  )}

                  {/* Code block */}
                  <div className="relative rounded-lg overflow-hidden">
                    <div className="absolute top-2 right-2 z-10">
                      <CopyButton
                        text={macro}
                        label={`Copy${macroCount > 1 ? ` Macro ${i + 1}` : ""}`}
                      />
                    </div>
                    <div className="bg-[#0d0d1a] rounded-lg p-3 font-mono text-sm leading-relaxed overflow-x-auto">
                      {macro.split("\n").map((line, j) => (
                        <MacroLine key={j} line={line} />
                      ))}
                    </div>
                  </div>

                  {/* Note between macros */}
                  {macroCount > 1 && i < macroCount - 1 && (
                    <p className="text-xs text-text-muted text-center mt-2 italic">
                      Run this macro, wait for it to finish, then run the next
                      one.
                    </p>
                  )}
                </div>
              ))}

            {/* Options */}
            <MacroOptionsSection options={options} onChange={setOptions} />
          </>
        )}
      </div>
    </>
  );

  // Desktop: slide-in right panel, Mobile: full-screen modal
  return createPortal(
    <div className="fixed inset-0 z-[80]">
      {/* Backdrop */}
      <div className="absolute inset-0 bg-black/40" onClick={onClose} />

      {/* Desktop panel (right side) */}
      <div className="hidden lg:flex absolute right-0 top-0 bottom-0 w-[420px] bg-bg-surface border-l border-border flex-col animate-slide-in-right">
        {content}
      </div>

      {/* Mobile modal (full screen) */}
      <div className="lg:hidden absolute inset-0 bg-bg-surface flex flex-col animate-slide-up">
        {content}
      </div>
    </div>,
    document.body,
  );
}
