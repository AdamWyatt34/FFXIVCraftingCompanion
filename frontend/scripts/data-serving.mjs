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
 * Remote only when the CDN answered with the manifest of the very data set the build has on disk;
 * anything else (a private repository's 404, a network error, a version mismatch) means the
 * deployment serves the data set itself, so a build never goes out pointing at files that are
 * not there.
 */
export function decideServing({ forceLocal, base, probedVersion, expectedVersion }) {
  if (forceLocal) return { mode: "local", reason: "forced" };
  if (probedVersion === null) return { mode: "local", reason: `${base}/manifest.json could not be read` };
  if (probedVersion !== expectedVersion) {
    return { mode: "local", reason: `${base} serves data set ${probedVersion}, the lock expects ${expectedVersion}` };
  }
  return { mode: "remote", reason: null };
}
