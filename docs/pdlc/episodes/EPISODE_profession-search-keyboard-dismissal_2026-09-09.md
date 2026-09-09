# Episode 017: Profession Search Keyboard Dismissal

**Feature:** F-034 `profession-search-keyboard-dismissal`
**Beads:** `agenda-buddy-26r`
**Date shipped:** 2026-09-09
**Version:** `v0.18.0`
**PR:** #152

## Problem

The provider Professions screen pins Save below the catalog, but iOS placed the software keyboard over it after
the provider typed into Search professions. Search had no submit handler and the page did not dismiss soft input
on background taps, leaving no reliable route back to Save.

## Change

- `ContentPage.HideSoftInputOnTapped="True"` dismisses the keyboard when the provider taps outside search.
- `SearchBar.ReturnType="Search"` gives the keyboard a clear completion action.
- `SearchButtonPressed` unfocuses the SearchBar without clearing the query or changing selected professions.
- A structural test permanently guards both dismissal paths and query preservation.

No view-model, API, persistence, or layout behavior changed.

## Verification

- Focused keyboard and pinned-action tests: 8 passed.
- Full mobile suite: 874 passed, 7 intentional live-Identity skips.
- Android Debug build: passed.
- iOS Debug build: passed.
- iPhone 17 Pro simulator: reproduced the keyboard covering Save, then verified both Search-key dismissal and
  background-tap dismissal preserve the query and restore the pinned Save button.