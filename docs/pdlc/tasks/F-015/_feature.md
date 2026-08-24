---
id: F-015
title: api-gateway-and-mobile-contract
status: shipped
priority: 17
labels: [roadmap, "priority:17"]
claimed_by:
created: 2026-08-15
updated: 2026-08-24
shipped: 2026-08-24
episode: docs/pdlc/episodes/EPISODE_api-gateway-and-mobile-contract_2026-08-24.md
version: v0.5.0
pr: "#41"
---
Make the mobile client actually reach the backend. Three compounding faults: (1) every MobileApp domain path omits the api/v1/ prefix and targets routes that do not exist (GET booking?date= - Booking has no GET at all); (2) a single ApiBaseUrl cannot address 7 ports and no gateway exists; (3) all three configured/fallback base URLs point at no service or at Identity. The SeedDataProvider fallback silently masks all of it, which is why F-012 looks shipped. Also wire the unused refresh-token flow (stored, never used - hard logout at 60 min) and make LogoutAsync call the server. Source: docs/pdlc/context/16-mobile-client.md, 01-api-surface.md. Depends on F-013.
