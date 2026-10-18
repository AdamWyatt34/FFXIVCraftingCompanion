# Security policy

## Reporting a vulnerability

Please report security issues privately through GitHub's
[private vulnerability reporting](https://github.com/AdamWyatt34/FFXIVCraftingCompanion/security/advisories/new)
for this repository, not in a public issue. Include what you found, how to reproduce it and
what you think the impact is. You will get an acknowledgement within a week; fixes ship as
ordinary releases and the advisory is published once a fix is live.

## Scope

- The site at ffxivcrafting.app and the API it uses for accounts.
- This repository's code: the .NET API, the Next.js frontend, the Rust engine and the export.

Game data on the site is a static export; the account API holds account data only (email,
password hash, Discord id, character profile, lists, rotations). There is no bug bounty.

## Supported versions

Only the `main` branch is supported; the site always runs its latest deploy.
