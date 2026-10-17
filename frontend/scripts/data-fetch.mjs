// Places the exported game data under public/data/v/<version>/ before `next build`, and records
// the version in public/data/current.json for next.config.ts. Sources, in order:
//   FFXIV_DATA_DIR   a local export (e.g. e2e/fixtures/data, or `dotnet run --project
//                    src/FFXIV.Export`); used as is
//   data.lock.json   the commit of the `data` branch pinned by the Data Export workflow,
//                    downloaded as a tarball from codeload.github.com
// With neither, the build proceeds without game data (no recipe pages, no search) and says so.

import { execFileSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const frontendDir = resolve(fileURLToPath(import.meta.url), "..", "..");
const publicData = join(frontendDir, "public", "data");
const lockPath = join(frontendDir, "data.lock.json");
const repo = process.env.FFXIV_DATA_REPO ?? "AdamWyatt34/FFXIVCraftingCompanion";

function log(message) {
  console.log(`[data-fetch] ${message}`);
}

function readManifest(dir) {
  const path = join(dir, "manifest.json");
  if (!existsSync(path)) throw new Error(`${dir} has no manifest.json`);
  return JSON.parse(readFileSync(path, "utf8"));
}

function install(sourceDir, expectedVersion) {
  const manifest = readManifest(sourceDir);
  if (expectedVersion && manifest.version !== expectedVersion) {
    throw new Error(`data set is ${manifest.version}, lock expects ${expectedVersion}`);
  }
  const target = join(publicData, "v", manifest.version);
  rmSync(publicData, { recursive: true, force: true });
  mkdirSync(target, { recursive: true });
  cpSync(sourceDir, target, { recursive: true });
  writeFileSync(
    join(publicData, "current.json"),
    JSON.stringify({ version: manifest.version, bucketSize: manifest.bucketSize, gameVersion: manifest.gameVersion }) + "\n",
  );
  log(`installed ${manifest.version} (game ${manifest.gameVersion}, ${manifest.counts.recipes} recipes, ${manifest.counts.items} items)`);
}

function download(lock) {
  const work = mkdtempSync(join(tmpdir(), "ffxiv-data-"));
  try {
    const tarball = join(work, "data.tar.gz");
    const url = `https://codeload.github.com/${repo}/tar.gz/${lock.dataSha}`;
    log(`downloading ${url}`);
    execFileSync("curl", ["-fsSL", "--retry", "3", "-o", tarball, url], { stdio: "inherit" });
    const extracted = join(work, "x");
    mkdirSync(extracted);
    execFileSync("tar", ["-xzf", tarball, "-C", extracted, "--strip-components=1"], { stdio: "inherit" });
    install(extracted, lock.version);
  } finally {
    rmSync(work, { recursive: true, force: true });
  }
}

const localDir = process.env.FFXIV_DATA_DIR;
if (localDir) {
  install(resolve(frontendDir, localDir), null);
} else if (existsSync(lockPath)) {
  download(JSON.parse(readFileSync(lockPath, "utf8")));
} else {
  rmSync(publicData, { recursive: true, force: true });
  log("no FFXIV_DATA_DIR and no data.lock.json: building without game data");
}
