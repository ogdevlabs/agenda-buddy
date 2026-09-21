# Episode 023: Release Catch-Up (v0.24.0 → v0.25.0)

**Date:** 2026-09-21
**Version:** `v0.25.0`
**Status:** Final
**Bead:** none filed for this episode itself (see per-PR notes below)
**PRs:** #172, #173, #174, #175, #176, #177

## Intent

Not a single planned feature — a retroactive catch-up. Six PRs merged to `main` after `v0.24.0` (2026-09-13)
without running this project's Ship sub-phase, so none of them produced a tag, a GitHub Release, or a
changelog entry. This episode exists only to restore that invariant: every commit on `main` traceable to a
tagged, released version. The user asked directly why the last several merges produced no tag/release; this
is the answer and the fix, not a description of new product work planned as one unit.

## Changes

Summarizing the six merged PRs, in merge order:

- **#172 — `fix(ci): handle inert-only dev drift`** (Bead `agenda-buddy-5ns`). The scheduled dev-environment
  drift check ran under `bash -e`/`pipefail` and treated an expected-empty `grep -Ev` result (every changed
  path inert) as a pipeline failure, so a genuinely healthy "nothing to deploy" run appeared red and skipped
  the heal step. Fixed both grep pipelines to succeed on an expected-empty result; added a structural
  regression test.
- **#173 — `Fix provider booking flow and standardize app branding`.** Served fresh provider
  profession/service data across service processes instead of a stale cross-process read; allowed paid
  booking and confirmation through the non-charging recording payment gateway in local development;
  standardized auth/header/app-icon on the AgendaMe "AM" mark; adopted the MAUI UIScene lifecycle and
  required background modes for iOS 27.
- **#174 — `Use stylish calendar artwork for AgendaMe branding`.** Replaced the interim "AM" monogram with a
  calendar-mark vector shared by in-app UI, the adaptive app icon, and the splash screen; added tests
  enforcing icon geometry and splash/mark equality.
- **#175 — `Fix paid bookings in deployed dev`.** The deployed dev environment (Cloud AppHost shape) received
  Stripe's unconfigured sentinel and fell back to the fail-closed payment gateway, rejecting every paid
  booking. Added explicit `Payments:Mode` configuration with Stripe precedence, wired `payments-mode` into
  Booking for the Cloud AppHost shape (`Recording` for `dev` only; staging/production stay `Unconfigured`),
  returned typed booking results instead of collapsing every non-2xx response to a generic "stale slot," and
  narrowed availability refresh to genuine overlap conflicts.
- **#176 — `Fix Customer option unselectable on registration role picker`.** `RegisterPage`'s Customer radio
  had no `IsChecked` binding and neither radio shared a `GroupName`, so it couldn't be selected and nothing
  enforced Provider/Customer mutual exclusion. Bound Customer's `IsChecked` to the inverse of `IsProvider`,
  grouped both radios, and added highlight triggers so the selected card's styling matches the real
  selection. Verified live in the iOS simulator, including a full register → verify → login → provider
  onboarding (payout → professions → add service) run.
- **#177 — `Add Tattoo Artist profession with Spanish localization`.** Added "Tattoo Artist"/"Tatuador" to the
  profession catalog (seed data, mobile display-name mapping, both resx files). Also changed
  `ProfessionSeedHostedService` from "insert the whole seed batch only if the collection is empty" to
  upsert-by-name on every startup — the former meant a profession added after first deploy could never reach
  an already-seeded database (every deployed environment) without a manual write, since this codebase has no
  migration framework. The fix makes future catalog additions reach deployed environments through the normal
  deploy pipeline alone.

## Process gap this episode documents (not fixed retroactively)

- `docs/pdlc/memory/CHANGELOG.md` was already stale before this episode — its last entry is `v0.7.0`
  (2026-08-26), seventeen versions behind `v0.24.0`. This episode adds only its own `v0.25.0` entry; the
  seventeen missing entries between `v0.7.0` and `v0.24.0` are not reconstructed here, matching this
  project's own established handling of exactly this kind of gap (see `episodes/index.md`'s note on episodes
  006–007, and `DEPLOYMENTS.md`'s "`v0.6.0` shipped without a matching skip row here — gap noted in passing,
  not backfilled").
- `docs/pdlc/memory/DEPLOYMENTS.md`'s cloud-deploy-skip log has the same shape of gap, last entry `v0.9.0`.
  Left untouched for the same reason — reconstructing fifteen versions of deploy-skip history with no
  first-hand record of each would be fabrication, not documentation.
- Why the gap happened at all: PRs #172–#177 were each merged as an ordinary PR-merge-to-main (git + GitHub
  REST API), without invoking this project's Ship sub-phase, which is a deliberate, separate step — not
  something CI performs automatically on merge. "Ship" in casual instruction was read as "merge the PR,"
  not as "tag, release, and record."

## Verification

No new product code in this episode — it is documentation plus the tag/release bookkeeping. The six PRs it
covers were each already verified (backend/mobile/integration suites, live simulator checks) in their own
PR description, quoted above; nothing here re-runs them.

## Ship

- This episode's own change (episode file, `episodes/index.md`, `CHANGELOG.md`) is committed directly to
  `main` as PDLC bookkeeping, per this project's convention for `docs(pdlc): finalize vX.Y.Z episode` commits
  (e.g. PR #171 for `v0.24.0`).
- Annotated tag `v0.25.0` created on that commit and pushed.
- A matching GitHub Release published from the tag.
