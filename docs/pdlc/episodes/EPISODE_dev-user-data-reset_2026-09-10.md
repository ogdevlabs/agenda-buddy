# Episode 019: Dev User-Data Reset

**Feature:** `dev-user-data-reset`
**Beads:** `agenda-buddy-47x`
**Date shipped:** 2026-09-10
**Version:** `v0.20.0`
**PRs:** #157, #158, #159, #160

## Problem

Stripe payment-method and provider-payout onboarding changed the persisted account contract. Existing dev users
could not reliably exercise the new registration path, and deleting credentials alone would leave issued access
tokens valid for up to one hour.

## Change

- Added a manual, main-only reset workflow with an exact destructive confirmation and audit reason.
- Reused the existing dev stop workflow and waited for all Container App replicas to drain before touching data.
- Deleted every user document from `agenda_buddy` and `IdentityDb`, preserving collections, indexes and the seeded
  profession catalogue.
- Added issued-at claims and durable global JWT cutoffs in both databases, invalidating every pre-reset access and
  refresh token while allowing new registrations.
- Split long replica polling into bounded slices with fresh Azure OIDC logins before each retry.
- Restored the environment only after successful purge verification and only when required by the dev schedule.

## Live Verification

- PR #157 CI: backend, integration, security and all seven container scans green.
- Main run `34499095151`: deployment and Gateway smoke test green.
- First reset failed closed before deleting data because the original drain window was too short.
- PR #158 extended drain handling and made `dev-env-stop.yml` the reusable stop entry point.
- Reset run `34502591982` deleted and verified all user data, preserved 115 professions, and restored dev.
- Independent Atlas verification found zero user documents and exposed a missing IdentityDb token cutoff.
- PR #159 added the second cutoff; the first retry failed before MongoDB because its long Azure polling step
  outlived the federated client assertion.
- PR #160 split drain and purge into separately authenticated jobs. Final verification confirmed both databases
  contain their cutoff marker and no user documents remain.

## Operational Boundary

The reset deletes AgendaMe data only. Stripe sandbox Customers, PaymentIntents and Express accounts are external
records and remain until the sandbox is separately reset or replaced.