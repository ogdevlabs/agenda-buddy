# PRD: F-034 — Profession Search Keyboard Dismissal

**Feature ID:** F-034
**Beads:** `agenda-buddy-26r`
**Date:** 2026-09-09
**Status:** Complete — pending Ship

## Problem

On iOS, focusing the provider profession search raises the software keyboard over the pinned Save action. The
screen provides no reliable dismissal gesture or keyboard action, so a provider can filter and select a
profession but cannot reach Save without leaving the flow.

## Requirements

1. Tapping outside the search input dismisses the software keyboard.
2. Pressing the keyboard Search action dismisses it explicitly.
3. Dismissal preserves the current query and profession selections.
4. Save and Continue remain pinned outside the catalog scroller.
5. The behavior is covered structurally and verified on an iOS simulator with the software keyboard visible.

## Acceptance Evidence

- `ProfessionsKeyboardDismissalTest` guards both dismissal paths and query preservation.
- `PinnedActionTest` confirms Save and Continue remain outside all scrollers.
- Full mobile suite: 874 passed, 7 intentional acceptance-test skips.
- Android Debug and iOS Debug builds pass.
- iPhone 17 Pro simulator: keyboard reproduced covering Save; pressing Search dismissed it while preserving the
  query and restoring Save.

## Design

Use MAUI's native `ContentPage.HideSoftInputOnTapped="True"` for background taps. Give the `SearchBar` a
`ReturnType="Search"` and handle `SearchButtonPressed` by calling `Unfocus()`. No platform-specific handler,
dependency, view-model mutation, or layout workaround is required.