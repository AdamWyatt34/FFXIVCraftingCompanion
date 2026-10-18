// Places the exported game data where the build and the site read it, before `next build`.
//
// Sources, in order:
//   FFXIV_DATA_DIR   a local export (e.g. e2e/fixtures/data, or `dotnet run --project
//                    src/FFXIV.Export`), copied under public/data/v/<version>/ and served by the
//                    deployment itself.
//   data.lock.json   the commit of the `data` branch pinned by the Data Export workflow,
//                    downloaded as a tarball into .data/v/<version>/ for the build's server-side
//                    reads (sitemap, hubs, prerendered pages). The browser and on-demand renders
//                    read the same commit from a CDN (jsDelivr over the public repository by
//                    default), so the data set, hundreds of MB, is not part of the deployment and
//                    does not count against Vercel's Deployment Storage. When the CDN cannot
//                    serve it (the repository is private, or the probe fails for any reason) the
//                    data set is copied under public/ and served by the deployment as before,
//                    with a warning in the build log. The tarball download needs a token with read
//                    access to the repository's contents in GITHUB_TOKEN while the repository is
//                    private (GitHub Actions passes its own; Vercel needs a fine-grained PAT in the
//                    project's environment); a public repository needs none.
// With neither source, the build proceeds without game data (no recipe pages, no search).
//
// Knobs: FFXIV_DATA_BASE_URL replaces the default CDN base (any origin serving the data set's
// files with CORS); FFXIV_DATA_SERVE=local always ships the data set in the deployment;
// FFXIV_DATA_REPO names the repository (owner/name).
//
// Writes .data/current.json for next.config.ts:
//   { version, bucketSize, gameVersion, base, dir }
// where `base` is what the browser prefixes to a data path (a URL, or /data/v/<version> when the
// deployment serves it) and `dir` is where the build reads the files from disk.

import { execFileSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { decideServing, defaultDataBase } from "./data-serving.mjs";

const frontendDir = resolve(fileURLToPath(import.meta.url), "..", "..");
const publicData = join(frontendDir, "public", "data");
const dataRoot = join(frontendDir, ".data");
const lockPath = join(frontendDir, "data.lock.json");
const repo = process.env.FFXIV_DATA_REPO ?? "AdamWyatt34/FFXIVCraftingCompanion";

function log(message) {
  console.log(`[data-fetch] ${message}`);
}

function warn(message) {
  console.warn(`[data-fetch] WARNING: ${message}`);
}

function readManifest(dir) {
  const path = join(dir, "manifest.json");
  if (!existsSync(path)) throw new Error(`${dir} has no manifest.json`);
  return JSON.parse(readFileSync(path, "utf8"));
}

function writeCurrent(manifest, base, dir) {
  mkdirSync(dataRoot, { recursive: true });
  writeFileSync(
    join(dataRoot, "current.json"),
    JSON.stringify({
      version: manifest.version,
      bucketSize: manifest.bucketSize,
      gameVersion: manifest.gameVersion,
      base,
      dir,
    }) + "\n",
  );
}

function describe(manifest) {
  return `${manifest.version} (game ${manifest.gameVersion}, ${manifest.counts.recipes} recipes, ${manifest.counts.items} items)`;
}

/** Copies the data set under public/ so the deployment serves it at /data/v/<version>/. */
function serveFromDeployment(sourceDir, manifest) {
  const target = join(publicData, "v", manifest.version);
  mkdirSync(target, { recursive: true });
  cpSync(sourceDir, target, { recursive: true });
  writeCurrent(manifest, `/data/v/${manifest.version}`, target);
  log(`installed ${describe(manifest)} under public/data; the deployment serves it`);
}

/** The manifest the CDN serves for this base, or null with the reason it could not be read. */
function probe(base) {
  const work = mkdtempSync(join(tmpdir(), "ffxiv-probe-"));
  try {
    const file = join(work, "manifest.json");
    execFileSync("curl", ["-fsSL", "--max-time", "30", "-o", file, `${base}/manifest.json`], { stdio: ["ignore", "ignore", "pipe"] });
    return { manifest: JSON.parse(readFileSync(file, "utf8")), reason: null };
  } catch (err) {
    return { manifest: null, reason: err instanceof Error ? err.message.split("\n")[0] : String(err) };
  } finally {
    rmSync(work, { recursive: true, force: true });
  }
}

function download(lock) {
  const work = mkdtempSync(join(tmpdir(), "ffxiv-data-"));
  try {
    const tarball = join(work, "data.tar.gz");
    const token = process.env.GITHUB_TOKEN ?? process.env.GH_TOKEN;
    // The API endpoint answers with a redirect to a signed codeload URL; curl drops the
    // Authorization header on that cross-host redirect, which is fine, the signed URL needs none.
    const url = token
      ? `https://api.github.com/repos/${repo}/tarball/${lock.dataSha}`
      : `https://codeload.github.com/${repo}/tar.gz/${lock.dataSha}`;
    const auth = token ? ["-H", `Authorization: Bearer ${token}`] : [];
    log(`downloading ${url}${token ? " (authenticated)" : ""}`);
    execFileSync("curl", ["-fsSL", "--retry", "3", ...auth, "-o", tarball, url], { stdio: "inherit" });
    const dir = join(dataRoot, "v", lock.version);
    rmSync(join(dataRoot, "v"), { recursive: true, force: true });
    mkdirSync(dir, { recursive: true });
    execFileSync("tar", ["-xzf", tarball, "-C", dir, "--strip-components=1"], { stdio: "inherit" });
    const manifest = readManifest(dir);
    if (manifest.version !== lock.version) {
      throw new Error(`data set is ${manifest.version}, lock expects ${lock.version}`);
    }
    return { dir, manifest };
  } finally {
    rmSync(work, { recursive: true, force: true });
  }
}

function serveLocked(lock) {
  const { dir, manifest } = download(lock);
  const base = process.env.FFXIV_DATA_BASE_URL?.replace(/\/+$/, "") || defaultDataBase(repo, lock.dataSha);
  const probed = process.env.FFXIV_DATA_SERVE === "local" ? null : probe(base);
  const decision = decideServing({
    forceLocal: process.env.FFXIV_DATA_SERVE === "local",
    base,
    probedVersion: probed?.manifest?.version ?? null,
    expectedVersion: manifest.version,
  });
  if (decision.mode === "remote") {
    writeCurrent(manifest, base, dir);
    log(`downloaded ${describe(manifest)} for the build; the site reads it from ${base}`);
    return;
  }
  if (decision.reason === "forced") log("FFXIV_DATA_SERVE=local: the deployment serves the data set");
  else warn(`${decision.reason}${probed?.reason ? ` (${probed.reason})` : ""}; falling back to serving the data set from the deployment, which adds its full size to this deployment`);
  serveFromDeployment(dir, manifest);
}

// A stale copy from an earlier build must never ship by accident.
rmSync(publicData, { recursive: true, force: true });
rmSync(join(dataRoot, "current.json"), { force: true });

const localDir = process.env.FFXIV_DATA_DIR;
if (localDir) {
  const source = resolve(frontendDir, localDir);
  serveFromDeployment(source, readManifest(source));
} else if (existsSync(lockPath)) {
  serveLocked(JSON.parse(readFileSync(lockPath, "utf8")));
} else {
  log("no FFXIV_DATA_DIR and no data.lock.json: building without game data");
}
