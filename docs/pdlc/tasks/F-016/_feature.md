---
id: F-016
title: secure-public-endpoints
status: shipped
priority: 14
labels: [roadmap, "priority:14"]
claimed_by: null
created: 2026-08-15
updated: 2026-08-22
---
Close the PII exposure. Six anonymous endpoints leak data, worst being GET /api/v1/providers which returns every provider's full record including embedded AppointmentEntities with customer emails and SubscribedCustomerCollection - unauthenticated and unpaginated. Also: both Calendar routes are authenticated but NOT ownership-guarded (any user reads any provider's appointments - IDOR); OwnershipGuard.AssertRole is never called so the role claim authorizes nothing; add pagination to both list endpoints; map ForbiddenException to 403 centrally so a forgotten try/catch cannot become a 500. Source: docs/pdlc/context/13-security.md, 01-api-surface.md, threat-model.md inherited exposures.
