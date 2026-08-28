---
id: F-024
title: data-subject-rights
status: shipped
priority: 24
labels: [roadmap, "priority:24"]
claimed_by: null
created: 2026-08-18
updated: 2026-08-28
---
No data-subject-rights capability exists: no export, no deletion, no anonymisation. The deeper problem is that deletion is not currently possible even manually, because the same record is stored in three places. BookingService.CancelAppointmentAsync hard-deletes an appointment from the appointments collection, but the same appointment ALSO persists embedded inside the provider document (ProviderEntity.cs:40-42 embeds AppointmentEntities) and AGAIN inside the events audit blobs, where every command and every query serialises its full payload as a JSON string (Event.cs:14). So "delete" leaves at least two copies, and any erasure request is unsatisfiable today.

Compounding it: the events collection has no retention policy, no pruning, no index, and no actor field - and GetProvidersQueryHandler.cs:23 serialises the ENTIRE provider list into it on every read. F-016 stops that write; this feature would deal with what has already accumulated.

Also absent: field-level encryption. NoteEntity.content holds providers' private therapy and coaching session notes - the most sensitive data in the product - with no CSFLE, no Queryable Encryption, and no encryption at rest beyond whatever Atlas provides by default. No DPA, retention schedule, or privacy policy is committed.

DEFERRED, and deliberately so: the maintainer confirmed on 2026-08-18 that the cluster holds only synthetic/development data, never real people's records. So there is no live personal-data obligation and no GDPR clock. This becomes urgent the moment real user data lands - which makes it a hard prerequisite for any production launch, not merely a backlog item.

Source: docs/pdlc/context/13-security.md:212-218, 15-cqrs-and-messaging.md, docs/pdlc/brainstorm/brainstorm_platform-remediation_2026-08-18.md.
