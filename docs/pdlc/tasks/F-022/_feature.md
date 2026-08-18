---
id: F-022
title: password-reset-flow
status: planned
priority: 22
labels: [roadmap, "priority:22"]
claimed_by: null
created: 2026-08-18
updated: 2026-08-18
---
No password reset, change, or forced-reset flow exists anywhere in the solution. CredentialEntity.cs:29 declares MustResetPassword and SeedAuthCredentials.cs:68 sets it, but LoginAsync (IdentityService.cs:79-121) never inspects it - so the forced-reset flow the field exists for does not exist. There is no password-reset or change-password endpoint at all (01-api-surface.md), which means a user who forgets their password has NO recovery path.

Filed rather than absorbed into the Platform Remediation program at Discover 2026-08-18, for a specific reason: this is a NEW CAPABILITY, not a defect fix. It needs endpoints, single-use reset tokens with expiry, and a delivery channel. Delivery means NotificationService - which is one of the six unreachable services that F-014 wires. So this is genuinely downstream of F-014, not something that could have been folded into the security work.

Depends on: F-014 (NotificationService must be registered and routed before a reset email can be sent).

Source: docs/pdlc/context/13-security.md:111, docs/pdlc/brainstorm/brainstorm_platform-remediation_2026-08-18.md.
