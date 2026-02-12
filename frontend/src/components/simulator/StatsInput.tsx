"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import { User, Pencil } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { useProfile } from "@/hooks/useProfile";
import { CRAFT_TYPE_INFO, SIMULATOR_CONSTANTS } from "@/lib/constants";
import { LEVEL_PRESETS } from "@/lib/gear-constants";
import type { CrafterStatsInput } from "@/types/simulator";
import type { CraftType } from "@/types/api";

interface StatsInputProps {
  stats: CrafterStatsInput | null;
  onStatsChange: (stats: CrafterStatsInput) => void;
  selectedCraftType: CraftType | null;
}

interface LocalStats {
  level: string;
  craftsmanship: string;
  control: string;
  cp: string;
  isSpecialist: boolean;
}

interface ValidationErrors {
  level?: string;
  craftsmanship?: string;
  control?: string;
  cp?: string;
}

function toLocalStats(stats: CrafterStatsInput): LocalStats {
  return {
    level: String(stats.level),
    craftsmanship: String(stats.craftsmanship),
    control: String(stats.control),
    cp: String(stats.cp),
    isSpecialist: stats.isSpecialist ?? false,
  };
}

function validateStats(local: LocalStats): ValidationErrors {
  const errors: ValidationErrors = {};
  const level = parseInt(local.level, 10);
  const craftsmanship = parseInt(local.craftsmanship, 10);
  const control = parseInt(local.control, 10);
  const cp = parseInt(local.cp, 10);

  if (!local.level || isNaN(level) || level < 1 || level > 100) {
    errors.level = "Level must be between 1 and 100";
  }
  if (!local.craftsmanship || isNaN(craftsmanship) || craftsmanship <= 0) {
    errors.craftsmanship = "Craftsmanship must be greater than 0";
  }
  if (!local.control || isNaN(control) || control <= 0) {
    errors.control = "Control must be greater than 0";
  }
  if (local.cp === "" || isNaN(cp) || cp < 0) {
    errors.cp = "CP cannot be negative";
  }

  return errors;
}

function parseStats(local: LocalStats): CrafterStatsInput | null {
  const errors = validateStats(local);
  if (Object.keys(errors).length > 0) return null;
  return {
    level: parseInt(local.level, 10),
    craftsmanship: parseInt(local.craftsmanship, 10),
    control: parseInt(local.control, 10),
    cp: parseInt(local.cp, 10),
    isSpecialist: local.isSpecialist,
  };
}

export function StatsInput({
  stats,
  onStatsChange,
  selectedCraftType,
}: StatsInputProps) {
  const { isAuthenticated } = useAuth();
  const { profile } = useProfile();

  const hasProfile =
    isAuthenticated && profile !== null && profile.stats.craftsmanship > 0;

  const [mode, setMode] = useState<"manual" | "profile">(
    hasProfile ? "profile" : "manual",
  );
  const [localStats, setLocalStats] = useState<LocalStats>(
    stats
      ? toLocalStats(stats)
      : { level: "", craftsmanship: "", control: "", cp: "", isSpecialist: false },
  );
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [touched, setTouched] = useState<Record<string, boolean>>({});
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Sync local stats when external stats prop changes (e.g., profile auto-fill)
  useEffect(() => {
    if (stats && mode === "profile") {
      setLocalStats(toLocalStats(stats));
    }
  }, [stats, mode]);

  // Update mode when profile becomes available
  useEffect(() => {
    if (hasProfile && !stats) {
      setMode("profile");
    }
  }, [hasProfile, stats]);

  // Profile mode: propagate stats from profile
  useEffect(() => {
    if (mode !== "profile" || !profile) return;

    const jobLevel = getProfileJobLevel();
    const profileStats: CrafterStatsInput = {
      level: jobLevel,
      craftsmanship: profile.stats.craftsmanship,
      control: profile.stats.control,
      cp: profile.stats.cp,
    };
    onStatsChange(profileStats);
    setLocalStats(toLocalStats(profileStats));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode, profile, selectedCraftType]);

  // Debounced propagation for manual mode
  const propagateDebounced = useCallback(
    (newLocal: LocalStats) => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
      debounceRef.current = setTimeout(() => {
        const parsed = parseStats(newLocal);
        if (parsed) {
          onStatsChange(parsed);
        }
      }, SIMULATOR_CONSTANTS.DEBOUNCE_STATS_MS);
    },
    [onStatsChange],
  );

  // Cleanup debounce on unmount
  useEffect(() => {
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, []);

  function getProfileJobLevel(): number {
    if (!profile) return 1;
    if (selectedCraftType && profile.jobLevels[selectedCraftType]) {
      return profile.jobLevels[selectedCraftType].level;
    }
    return profile.activeJobLevel;
  }

  function handleFieldChange(field: keyof LocalStats, value: string) {
    // Only allow numeric input (or empty)
    if (value !== "" && !/^\d+$/.test(value)) return;

    const newLocal = { ...localStats, [field]: value };
    setLocalStats(newLocal);

    // Clear error on edit
    if (errors[field as keyof ValidationErrors]) {
      setErrors((prev) => {
        const next = { ...prev };
        delete next[field as keyof ValidationErrors];
        return next;
      });
    }

    propagateDebounced(newLocal);
  }

  function handleBlur(field: keyof ValidationErrors) {
    setTouched((prev) => ({ ...prev, [field]: true }));
    const fieldErrors = validateStats(localStats);
    if (fieldErrors[field]) {
      setErrors((prev) => ({ ...prev, [field]: fieldErrors[field] }));
    }
  }

  function handleLevelPreset(level: number) {
    const newLocal = { ...localStats, level: String(level) };
    setLocalStats(newLocal);
    setErrors((prev) => {
      const next = { ...prev };
      delete next.level;
      return next;
    });
    propagateDebounced(newLocal);
  }

  function handleSpecialistToggle() {
    const newLocal = { ...localStats, isSpecialist: !localStats.isSpecialist };
    setLocalStats(newLocal);
    propagateDebounced(newLocal);
  }

  function switchToManual() {
    setMode("manual");
    // Pre-fill manual inputs with current profile values
    if (stats) {
      setLocalStats(toLocalStats(stats));
    }
  }

  const craftInfo = selectedCraftType
    ? CRAFT_TYPE_INFO[selectedCraftType]
    : null;

  return (
    <div>
      {/* Mode toggle */}
      {hasProfile && (
        <div className="flex rounded-lg border border-border overflow-hidden mb-4">
          <button
            onClick={() => setMode("profile")}
            className={`flex-1 px-3 py-2 text-sm font-medium transition-colors ${
              mode === "profile"
                ? "bg-gold/15 text-gold"
                : "bg-bg-surface text-text-secondary hover:text-text-primary"
            }`}
          >
            Use Profile Stats
          </button>
          <button
            onClick={() => switchToManual()}
            className={`flex-1 px-3 py-2 text-sm font-medium transition-colors ${
              mode === "manual"
                ? "bg-gold/15 text-gold"
                : "bg-bg-surface text-text-secondary hover:text-text-primary"
            }`}
          >
            Enter Manually
          </button>
        </div>
      )}

      {/* Profile mode */}
      {mode === "profile" && profile ? (
        <div className="bg-bg-surface rounded-lg border border-border p-4">
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-2">
              <User size={16} className="text-gold" />
              <span className="text-sm font-medium text-text-primary">
                {profile.characterName}
              </span>
              {craftInfo && (
                <span
                  className={`text-xs px-2 py-0.5 rounded-full bg-bg-elevated ${craftInfo.colorClass}`}
                >
                  {craftInfo.name}
                </span>
              )}
            </div>
            <button
              onClick={switchToManual}
              className="text-xs text-text-secondary hover:text-gold transition-colors flex items-center gap-1"
            >
              <Pencil size={12} />
              Edit
            </button>
          </div>

          {profile.stats.craftsmanship > 0 ? (
            <div className="grid grid-cols-2 gap-3">
              <StatDisplay label="Level" value={getProfileJobLevel()} />
              <StatDisplay
                label="Craftsmanship"
                value={profile.stats.craftsmanship}
              />
              <StatDisplay label="Control" value={profile.stats.control} />
              <StatDisplay label="CP" value={profile.stats.cp} />
            </div>
          ) : (
            <p className="text-sm text-amber-400">
              No crafting stats found
              {selectedCraftType ? ` for ${craftInfo?.fullName ?? selectedCraftType}` : ""} on
              your profile. Enter stats manually or update your profile.
            </p>
          )}
        </div>
      ) : (
        /* Manual mode */
        <div className="space-y-4">
          {/* Level with presets */}
          <div>
            <label className="block text-sm font-medium text-text-secondary mb-1">
              Level
            </label>
            <div className="flex gap-2">
              <input
                type="text"
                inputMode="numeric"
                value={localStats.level}
                onChange={(e) => handleFieldChange("level", e.target.value)}
                onBlur={() => handleBlur("level")}
                placeholder="1-100"
                className="w-20 bg-bg-elevated border border-border focus:border-border-focus focus:outline-none text-text-primary rounded-lg px-3 py-2 transition-colors"
              />
              <div className="flex gap-1 flex-wrap">
                {LEVEL_PRESETS.map((lv) => (
                  <button
                    key={lv}
                    onClick={() => handleLevelPreset(lv)}
                    className={`px-2.5 py-1.5 text-xs rounded-md border transition-colors ${
                      localStats.level === String(lv)
                        ? "border-gold bg-gold/15 text-gold"
                        : "border-border bg-bg-elevated text-text-secondary hover:text-text-primary hover:border-border-focus"
                    }`}
                  >
                    {lv}
                  </button>
                ))}
              </div>
            </div>
            {touched.level && errors.level && (
              <p className="text-xs text-red-400 mt-1">{errors.level}</p>
            )}
          </div>

          {/* Stats grid */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-text-secondary mb-1">
                Craftsmanship
              </label>
              <input
                type="text"
                inputMode="numeric"
                value={localStats.craftsmanship}
                onChange={(e) =>
                  handleFieldChange("craftsmanship", e.target.value)
                }
                onBlur={() => handleBlur("craftsmanship")}
                placeholder="e.g., 4500"
                className="w-full bg-bg-elevated border border-border focus:border-border-focus focus:outline-none text-text-primary rounded-lg px-3 py-2 transition-colors"
              />
              {touched.craftsmanship && errors.craftsmanship && (
                <p className="text-xs text-red-400 mt-1">
                  {errors.craftsmanship}
                </p>
              )}
            </div>
            <div>
              <label className="block text-sm font-medium text-text-secondary mb-1">
                Control
              </label>
              <input
                type="text"
                inputMode="numeric"
                value={localStats.control}
                onChange={(e) => handleFieldChange("control", e.target.value)}
                onBlur={() => handleBlur("control")}
                placeholder="e.g., 4200"
                className="w-full bg-bg-elevated border border-border focus:border-border-focus focus:outline-none text-text-primary rounded-lg px-3 py-2 transition-colors"
              />
              {touched.control && errors.control && (
                <p className="text-xs text-red-400 mt-1">{errors.control}</p>
              )}
            </div>
            <div>
              <label className="block text-sm font-medium text-text-secondary mb-1">
                CP
              </label>
              <input
                type="text"
                inputMode="numeric"
                value={localStats.cp}
                onChange={(e) => handleFieldChange("cp", e.target.value)}
                onBlur={() => handleBlur("cp")}
                placeholder="e.g., 620"
                className="w-full bg-bg-elevated border border-border focus:border-border-focus focus:outline-none text-text-primary rounded-lg px-3 py-2 transition-colors"
              />
              {touched.cp && errors.cp && (
                <p className="text-xs text-red-400 mt-1">{errors.cp}</p>
              )}
            </div>
            <div className="flex items-end pb-2">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={localStats.isSpecialist}
                  onChange={handleSpecialistToggle}
                  className="w-4 h-4 rounded border-border bg-bg-elevated accent-gold"
                />
                <span className="text-sm text-text-secondary">Specialist</span>
              </label>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function StatDisplay({ label, value }: { label: string; value: number }) {
  return (
    <div className="bg-bg-elevated rounded-md p-2.5">
      <p className="text-xs text-text-muted">{label}</p>
      <p className="text-lg font-bold text-text-primary">
        {value.toLocaleString()}
      </p>
    </div>
  );
}
