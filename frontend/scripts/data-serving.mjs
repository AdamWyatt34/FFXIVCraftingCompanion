// The rules scripts/data-fetch.mjs applies when a locked data set could be served from a CDN
// instead of the deployment. Kept free of I/O so the unit tests can pin them.

/**
 * Where the data set's files live on the CDN for a data-branch commit: jsDelivr's GitHub endpoint,
 * which serves a public repository's files by commit with one-year immutable caching and keeps a
 * permanent copy of every file it has served, so a deployment keeps working after the Data Export
 * has moved the branch on.
 */
export function defaultDataBase(repo, sha) {
  return `https://cdn.jsdelivr.net/gh/${repo}@${sha}`;
}

/**
 * GitHub's own file endpoint for the same commit. jsDelivr stores every file it has served, but it
 * refuses to fetch files from a repository whose unpacked size is over its limit (this data set
 * is hundreds of MB), and which files make it through is not predictable. The raw endpoint has
 * no such limit and answers with CORS, but it caches for minutes rather than a year, so it is the
 * second origin the site tries, never the first. Null for a base that is not jsDelivr's GitHub
 * endpoint, such as FFXIV_DATA_BASE_URL.
 */
export function fallbackDataBase(base) {
  const match = /^https:\/\/cdn\.jsdelivr\.net\/gh\/([^/@]+\/[^/@]+)@([0-9a-f]{7,40})$/.exec(base);
  return match ? `https://raw.githubusercontent.com/${match[1]}/${match[2]}` : null;
}

/** How many of a data set's files the CDN may still refuse after warming before the build gives up on it. */
export function toleratedRefusals(total) {
  return Math.ceil(total * 0.05);
}

/**
 * Remote only when the CDN answered with the manifest of the very data set the build has on disk;
 * anything else (a private repository's 404, a network error, a version mismatch) means the
 * deployment serves the data set itself, so a build never goes out pointing at files that are
 * not there. `warmed`, when the build has fetched every file through the CDN, lists the files it
 * still refused: a few are tolerated (the site reads those from the fallback origin), more than
 * that means the CDN is not serving this data set and the deployment serves it instead.
 */
export function decideServing({ forceLocal, base, probedVersion, expectedVersion, warmed = null }) {
  if (forceLocal) return { mode: "local", reason: "forced" };
  if (probedVersion === null) return { mode: "local", reason: `${base}/manifest.json could not be read` };
  if (probedVersion !== expectedVersion) {
    return { mode: "local", reason: `${base} serves data set ${probedVersion}, the lock expects ${expectedVersion}` };
  }
  if (warmed && warmed.refused.length > 0) {
    const sample = warmed.refused.slice(0, 3).join(", ") + (warmed.refused.length > 3 ? ", …" : "");
    const refused = `${base} refused ${warmed.refused.length} of the data set's ${warmed.total} files after warming (${sample})`;
    if (warmed.refused.length > toleratedRefusals(warmed.total)) return { mode: "local", reason: refused };
    return { mode: "remote", reason: null, warning: `${refused}; the site reads those from the fallback origin` };
  }
  return { mode: "remote", reason: null, warning: null };
}
