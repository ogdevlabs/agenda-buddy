# Episode 016: Spanish Mobile Localization

**Feature:** F-033 `spanish-mobile-localization`
**Beads:** `agenda-buddy-d8l`
**Date shipped:** 2026-09-09
**Version:** `v0.17.0`

## Outcome

AgendaMe now runs in English or Latin-American Spanish (`es-MX`) from first launch through authenticated
provider and customer workflows. Language selection persists on-device and takes effect immediately by safely
reconstructing the Shell without duplicating route/event registration or losing session state.

The implementation includes 591 paired resource keys, all visible XAML/runtime/accessibility copy, 115 seeded
profession display mappings, operation-aware errors, 12 in-app/push notification types, device-locale push
registration, and paired 10-clause Terms and Privacy documents. Canonical API values and user-authored content
remain unchanged.

## Verification

- Mobile: 873 passed, 7 intentional live-Identity skips.
- Backend: 1,137 passed.
- Real MongoDB device-locale persistence: 2 passed.
- `dotnet format --verify-no-changes`: clean.
- Android Release: green; `es-MX` satellite resources and `localeConfig` packaged.
- iOS Release: green after clean rebuild; `es-MX` satellite resources and `CFBundleLocalizations` packaged.
- iPhone 17 Pro simulator: English login, Spanish login, signed-in provider dashboard, signed-in customer
  dashboard, role-dependent tabs, and Language page inspected with no clipping or overlap.
- Full integration suite was attempted repeatedly, but the execution wrapper terminated it around 135 seconds
  before summary; CI is the authoritative full-suite gate.

## Accepted Risk

Qualified legal and native Latin-American Spanish review has not signed off. The maintainer explicitly directed
shipping with that limitation disclosed. `docs/legal/SPANISH_LEGAL_REVIEW.md` remains the checklist and
`agenda-buddy-hff` is the durable follow-up. This episode does not claim legal approval.

## Release

The feature ships as the first GitHub Release in this repository. The release is created through the GitHub REST
API after PR CI and the post-merge Azure deploy are green; no `gh` command is used.