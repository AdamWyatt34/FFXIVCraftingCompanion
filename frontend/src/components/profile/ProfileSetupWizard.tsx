"use client";

import { useState } from "react";
import { Globe, PenLine, ArrowLeft, Search, ChevronDown, ChevronUp } from "lucide-react";
import Image from "next/image";
import { JobSelector } from "@/components/gear/JobSelector";
import { LevelInput } from "@/components/gear/LevelInput";
import { ItemIcon } from "@/components/ui/ItemIcon";
import { LoadingSpinner } from "@/components/ui/LoadingSpinner";
import { ServerSelector } from "./ServerSelector";
import {
  previewLodestone,
  importLodestone,
  createProfile,
  ApiClientError,
} from "@/lib/api-client";
import { JOB_INFO, DOH_JOBS, DOL_JOBS, getJobIconUrl, type GearJob } from "@/lib/gear-constants";
import { getDataCenterForServer } from "@/lib/server-data";
import type { LodestonePreviewDto } from "@/types/profile";

type WizardStep = "choose-method" | "lodestone-search" | "lodestone-confirm" | "manual";

interface ProfileSetupWizardProps {
  onComplete: () => void;
}

export function ProfileSetupWizard({ onComplete }: ProfileSetupWizardProps) {
  const [step, setStep] = useState<WizardStep>("choose-method");

  return (
    <div className="flex-1 overflow-y-auto p-4">
      <div className="max-w-lg mx-auto">
        {step === "choose-method" && <ChooseMethodStep onChoose={setStep} />}
        {(step === "lodestone-search" || step === "lodestone-confirm") && (
          <LodestoneFlow
            step={step}
            setStep={setStep}
            onComplete={onComplete}
          />
        )}
        {step === "manual" && (
          <ManualEntryFlow onBack={() => setStep("choose-method")} onComplete={onComplete} />
        )}
      </div>
    </div>
  );
}

function ChooseMethodStep({ onChoose }: { onChoose: (step: WizardStep) => void }) {
  return (
    <div className="space-y-6 animate-[fade-in_0.3s_ease-out]">
      <div className="text-center">
        <h2 className="text-xl font-bold text-text-primary mb-1">Set Up Your Profile</h2>
        <p className="text-text-secondary text-sm">How would you like to get started?</p>
      </div>

      <div className="space-y-3">
        <button
          onClick={() => onChoose("lodestone-search")}
          className="w-full p-4 rounded-lg border border-border bg-bg-surface hover:border-border-focus transition-all text-left group"
        >
          <div className="flex items-start gap-3">
            <div className="w-10 h-10 rounded-lg bg-blue-500/10 flex items-center justify-center shrink-0">
              <Globe className="text-blue-400" size={20} />
            </div>
            <div>
              <h3 className="font-semibold text-text-primary group-hover:text-gold transition-colors">
                Import from Lodestone
              </h3>
              <p className="text-sm text-text-secondary mt-0.5">
                Automatically import your character&apos;s jobs, gear, and stats from the FFXIV Lodestone
              </p>
            </div>
          </div>
        </button>

        <button
          onClick={() => onChoose("manual")}
          className="w-full p-4 rounded-lg border border-border bg-bg-surface hover:border-border-focus transition-all text-left group"
        >
          <div className="flex items-start gap-3">
            <div className="w-10 h-10 rounded-lg bg-gold/10 flex items-center justify-center shrink-0">
              <PenLine className="text-gold" size={20} />
            </div>
            <div>
              <h3 className="font-semibold text-text-primary group-hover:text-gold transition-colors">
                Enter Manually
              </h3>
              <p className="text-sm text-text-secondary mt-0.5">
                Type in your stats and job levels yourself
              </p>
            </div>
          </div>
        </button>
      </div>
    </div>
  );
}

function LodestoneFlow({
  step,
  setStep,
  onComplete,
}: {
  step: "lodestone-search" | "lodestone-confirm";
  setStep: (s: WizardStep) => void;
  onComplete: () => void;
}) {
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [server, setServer] = useState("");
  const [preview, setPreview] = useState<LodestonePreviewDto | null>(null);
  const [isSearching, setIsSearching] = useState(false);
  const [isImporting, setIsImporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSearch() {
    if (!firstName.trim() || !lastName.trim() || !server) return;

    setIsSearching(true);
    setError(null);

    try {
      const name = `${firstName.trim()} ${lastName.trim()}`;
      const result = await previewLodestone(name, server);
      setPreview(result);
      setStep("lodestone-confirm");
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(
          err.status === 404
            ? "Character not found. Check the name and server."
            : err.body.error || "Failed to search Lodestone.",
        );
      } else {
        setError("An unexpected error occurred.");
      }
    } finally {
      setIsSearching(false);
    }
  }

  async function handleImport() {
    if (!preview) return;

    setIsImporting(true);
    setError(null);

    try {
      const name = `${firstName.trim()} ${lastName.trim()}`;
      await importLodestone({ characterName: name, serverName: server });
      onComplete();
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.body.error || "Failed to import character.");
      } else {
        setError("An unexpected error occurred.");
      }
      setIsImporting(false);
    }
  }

  return (
    <div className="space-y-4 animate-[fade-in_0.3s_ease-out]">
      <button
        onClick={() => {
          setStep("choose-method");
          setPreview(null);
          setError(null);
        }}
        className="flex items-center gap-1 text-sm text-text-muted hover:text-text-secondary transition-colors"
      >
        <ArrowLeft size={16} />
        Back
      </button>

      <div>
        <h2 className="text-lg font-bold text-text-primary mb-1">Import from Lodestone</h2>
        <p className="text-text-secondary text-sm">
          Enter your character&apos;s name and server to search
        </p>
      </div>

      {step === "lodestone-search" && (
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label htmlFor="first-name" className="block text-sm font-medium text-text-secondary mb-1">
                First Name
              </label>
              <input
                id="first-name"
                type="text"
                required
                value={firstName}
                onChange={(e) => setFirstName(e.target.value)}
                className="w-full h-11 px-3 bg-bg-secondary border border-border rounded-lg text-text-primary placeholder:text-text-muted focus:outline-none focus:border-border-focus transition-colors"
                placeholder="Warrior"
              />
            </div>
            <div>
              <label htmlFor="last-name" className="block text-sm font-medium text-text-secondary mb-1">
                Last Name
              </label>
              <input
                id="last-name"
                type="text"
                required
                value={lastName}
                onChange={(e) => setLastName(e.target.value)}
                className="w-full h-11 px-3 bg-bg-secondary border border-border rounded-lg text-text-primary placeholder:text-text-muted focus:outline-none focus:border-border-focus transition-colors"
                placeholder="Of Light"
              />
            </div>
          </div>

          <div>
            <label htmlFor="server-select" className="block text-sm font-medium text-text-secondary mb-1">
              Server
            </label>
            <ServerSelector id="server-select" value={server} onChange={setServer} required />
          </div>

          {error && (
            <div className="p-3 rounded-lg bg-red-500/10 border border-red-500/30 text-red-400 text-sm">
              {error}
            </div>
          )}

          <button
            onClick={handleSearch}
            disabled={isSearching || !firstName.trim() || !lastName.trim() || !server}
            className="w-full h-11 bg-gold text-bg-primary font-semibold rounded-lg hover:bg-gold-light disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center justify-center gap-2"
          >
            {isSearching ? (
              <>
                <LoadingSpinner size={18} />
                Searching...
              </>
            ) : (
              <>
                <Search size={18} />
                Search Lodestone
              </>
            )}
          </button>
        </div>
      )}

      {step === "lodestone-confirm" && preview && (
        <div className="space-y-4">
          {/* Preview card */}
          <div className="p-4 bg-bg-elevated rounded-lg border border-gold/30 space-y-3">
            <div className="flex items-center gap-3">
              {preview.avatarUrl ? (
                <Image
                  src={preview.avatarUrl}
                  alt={preview.characterName}
                  width={56}
                  height={56}
                  className="w-14 h-14 rounded-full border-2 border-gold/40"
                />
              ) : (
                <div className="w-14 h-14 rounded-full bg-bg-surface flex items-center justify-center">
                  <Search className="text-text-muted" size={24} />
                </div>
              )}
              <div>
                <h3 className="font-bold text-text-primary">{preview.characterName}</h3>
                <p className="text-sm text-text-secondary">
                  {preview.serverName}
                  {preview.dataCenter && ` (${preview.dataCenter})`}
                </p>
                {preview.activeJobDisplayName && (
                  <p className="text-xs text-text-muted mt-0.5">
                    Active: {preview.activeJobDisplayName} Lv. {preview.activeJobLevel}
                  </p>
                )}
              </div>
            </div>

            {/* Job levels preview */}
            {Object.keys(preview.jobLevels).length > 0 && (
              <div className="pt-2 border-t border-border">
                <p className="text-xs text-text-muted mb-2">Detected jobs:</p>
                <div className="flex flex-wrap gap-2">
                  {Object.entries(preview.jobLevels)
                    .filter(([, jl]) => jl.level > 0)
                    .sort(([, a], [, b]) => b.level - a.level)
                    .slice(0, 11)
                    .map(([key, jl]) => {
                      const info = JOB_INFO[key as GearJob];
                      return (
                        <div key={key} className="flex items-center gap-1 text-xs">
                          {info && (
                            <ItemIcon
                              iconUrl={getJobIconUrl(info.classJobId)}
                              alt={jl.jobDisplayName}
                              size="sm"
                            />
                          )}
                          <span className={info?.colorClass ?? "text-text-secondary"}>
                            {key}
                          </span>
                          <span className="text-text-muted">{jl.level}</span>
                        </div>
                      );
                    })}
                </div>
              </div>
            )}
          </div>

          <p className="text-center text-sm text-text-secondary">Is this your character?</p>

          {error && (
            <div className="p-3 rounded-lg bg-red-500/10 border border-red-500/30 text-red-400 text-sm">
              {error}
            </div>
          )}

          <div className="flex gap-3">
            <button
              onClick={() => {
                setStep("lodestone-search");
                setPreview(null);
                setError(null);
              }}
              className="flex-1 h-11 border border-border text-text-primary rounded-lg hover:border-border-focus transition-colors"
            >
              Search Again
            </button>
            <button
              onClick={handleImport}
              disabled={isImporting}
              className="flex-1 h-11 bg-gold text-bg-primary font-semibold rounded-lg hover:bg-gold-light disabled:opacity-50 transition-colors flex items-center justify-center gap-2"
            >
              {isImporting ? (
                <>
                  <LoadingSpinner size={18} />
                  Importing...
                </>
              ) : (
                "Import Character"
              )}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

function ManualEntryFlow({
  onBack,
  onComplete,
}: {
  onBack: () => void;
  onComplete: () => void;
}) {
  const [characterName, setCharacterName] = useState("");
  const [server, setServer] = useState("");
  const [activeJob, setActiveJob] = useState<string | null>(null);
  const [activeJobLevel, setActiveJobLevel] = useState(90);
  const [craftsmanship, setCraftsmanship] = useState(0);
  const [control, setControl] = useState(0);
  const [cp, setCp] = useState(0);
  const [gathering, setGathering] = useState(0);
  const [perception, setPerception] = useState(0);
  const [gp, setGp] = useState(0);
  const [showMoreJobs, setShowMoreJobs] = useState(false);
  const [additionalJobs, setAdditionalJobs] = useState<Record<string, number>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isDoH = activeJob ? JOB_INFO[activeJob as GearJob]?.category === "DoH" : false;
  const isDoL = activeJob ? JOB_INFO[activeJob as GearJob]?.category === "DoL" : false;

  async function handleSubmit() {
    if (!activeJob) return;

    setIsSubmitting(true);
    setError(null);

    try {
      const jobLevels: Record<string, number> = { [activeJob]: activeJobLevel };
      for (const [job, level] of Object.entries(additionalJobs)) {
        if (level > 0) jobLevels[job] = level;
      }

      await createProfile({
        characterName: characterName.trim() || "Adventurer",
        serverName: server || undefined,
        dataCenter: server ? getDataCenterForServer(server) ?? undefined : undefined,
        activeJob,
        activeJobLevel,
        jobLevels,
        craftsmanship,
        control,
        cp,
        gathering,
        perception,
        gp,
      });
      onComplete();
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.body.error || "Failed to create profile.");
      } else {
        setError("An unexpected error occurred.");
      }
      setIsSubmitting(false);
    }
  }

  return (
    <div className="space-y-5 animate-[fade-in_0.3s_ease-out]">
      <button
        onClick={onBack}
        className="flex items-center gap-1 text-sm text-text-muted hover:text-text-secondary transition-colors"
      >
        <ArrowLeft size={16} />
        Back
      </button>

      <div>
        <h2 className="text-lg font-bold text-text-primary mb-1">Manual Setup</h2>
        <p className="text-text-secondary text-sm">Enter your character info and stats</p>
      </div>

      {/* Character name */}
      <div>
        <label htmlFor="char-name" className="block text-sm font-medium text-text-secondary mb-1">
          Character Name <span className="text-text-muted">(optional)</span>
        </label>
        <input
          id="char-name"
          type="text"
          value={characterName}
          onChange={(e) => setCharacterName(e.target.value)}
          className="w-full h-11 px-3 bg-bg-secondary border border-border rounded-lg text-text-primary placeholder:text-text-muted focus:outline-none focus:border-border-focus transition-colors"
          placeholder="Your character name"
        />
      </div>

      {/* Server */}
      <div>
        <label htmlFor="manual-server" className="block text-sm font-medium text-text-secondary mb-1">
          Server <span className="text-text-muted">(optional)</span>
        </label>
        <ServerSelector id="manual-server" value={server} onChange={setServer} />
      </div>

      {/* Primary job */}
      <div>
        <p className="text-sm font-medium text-text-secondary mb-2">Primary Job</p>
        <JobSelector selectedJob={activeJob} onJobSelect={setActiveJob} />
      </div>

      {activeJob && (
        <>
          {/* Level */}
          <LevelInput value={activeJobLevel} onChange={setActiveJobLevel} />

          {/* Stats */}
          <div className="space-y-3">
            <p className="text-sm font-medium text-text-secondary">
              Stats
              <span className="text-text-muted text-xs ml-2">
                (open Character window in-game to find these)
              </span>
            </p>

            {(isDoH || !isDoL) && (
              <div>
                <p className="text-xs uppercase tracking-wider text-text-muted mb-2">Crafting</p>
                <div className="grid grid-cols-3 gap-3">
                  <StatInput id="craftsmanship" label="Craftsmanship" value={craftsmanship} onChange={setCraftsmanship} />
                  <StatInput id="control" label="Control" value={control} onChange={setControl} />
                  <StatInput id="cp" label="CP" value={cp} onChange={setCp} />
                </div>
              </div>
            )}

            {(isDoL || !isDoH) && (
              <div>
                <p className="text-xs uppercase tracking-wider text-text-muted mb-2">Gathering</p>
                <div className="grid grid-cols-3 gap-3">
                  <StatInput id="gathering" label="Gathering" value={gathering} onChange={setGathering} />
                  <StatInput id="perception" label="Perception" value={perception} onChange={setPerception} />
                  <StatInput id="gp" label="GP" value={gp} onChange={setGp} />
                </div>
              </div>
            )}
          </div>

          {/* Additional jobs */}
          <div>
            <button
              onClick={() => setShowMoreJobs(!showMoreJobs)}
              className="flex items-center gap-1 text-sm text-text-muted hover:text-text-secondary transition-colors"
            >
              {showMoreJobs ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
              Add more job levels
            </button>

            {showMoreJobs && (
              <div className="mt-3 space-y-3 animate-[fade-in_0.2s_ease-out]">
                {[...DOH_JOBS, ...DOL_JOBS]
                  .filter((j) => j !== activeJob)
                  .map((job) => {
                    const info = JOB_INFO[job];
                    return (
                      <div key={job} className="flex items-center gap-3">
                        <ItemIcon
                          iconUrl={getJobIconUrl(info.classJobId)}
                          alt={info.fullName}
                          size="md"
                        />
                        <span className={`text-sm font-medium w-8 ${info.colorClass}`}>{job}</span>
                        <input
                          type="number"
                          min={0}
                          max={100}
                          value={additionalJobs[job] ?? ""}
                          onChange={(e) => {
                            const v = parseInt(e.target.value, 10);
                            setAdditionalJobs((prev) => ({
                              ...prev,
                              [job]: isNaN(v) ? 0 : Math.max(0, Math.min(100, v)),
                            }));
                          }}
                          placeholder="0"
                          className="w-16 h-9 px-2 text-center text-sm bg-bg-secondary border border-border rounded-lg text-text-primary focus:outline-none focus:border-border-focus transition-colors"
                        />
                        <span className="text-xs text-text-muted">{info.fullName}</span>
                      </div>
                    );
                  })}
              </div>
            )}
          </div>
        </>
      )}

      {error && (
        <div className="p-3 rounded-lg bg-red-500/10 border border-red-500/30 text-red-400 text-sm">
          {error}
        </div>
      )}

      <button
        onClick={handleSubmit}
        disabled={isSubmitting || !activeJob}
        className="w-full h-11 bg-gold text-bg-primary font-semibold rounded-lg hover:bg-gold-light disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center justify-center gap-2"
      >
        {isSubmitting ? (
          <>
            <LoadingSpinner size={18} />
            Creating Profile...
          </>
        ) : (
          "Create Profile"
        )}
      </button>
    </div>
  );
}

function StatInput({
  id,
  label,
  value,
  onChange,
}: {
  id: string;
  label: string;
  value: number;
  onChange: (v: number) => void;
}) {
  return (
    <div>
      <label htmlFor={id} className="block text-xs text-text-muted mb-1">
        {label}
      </label>
      <input
        id={id}
        type="number"
        min={0}
        max={99999}
        value={value || ""}
        onChange={(e) => {
          const v = parseInt(e.target.value, 10);
          onChange(isNaN(v) ? 0 : Math.max(0, v));
        }}
        className="w-full h-10 px-3 bg-bg-secondary border border-border rounded-lg text-text-primary text-center focus:outline-none focus:border-border-focus transition-colors"
        placeholder="0"
      />
    </div>
  );
}
