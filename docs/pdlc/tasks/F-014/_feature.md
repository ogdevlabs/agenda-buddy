---
id: F-014
title: wire-unreached-services
status: shipped
priority: 16
labels: [roadmap, "priority:16"]
claimed_by: null
created: 2026-08-15
updated: 2026-08-23
---
Register and route the six shipped-but-unreachable capabilities: NotificationService, MessageService, NoteService, PaymentService, ReportingService, and DeactivateProviderCommand. All six have domain implementations and unit tests but no DI registration, no configured collection, and no HTTP route - so F-006 through F-010 are marked Shipped while being unreachable. Also needs collection names in appsettings (no NotificationsCollection/MessagesCollection/NotesCollection/PaymentsCollection key exists) and a DI-registerable IPaymentGateway (StripePaymentGateway takes a raw string apiKey with no Stripe config section anywhere). Source: docs/pdlc/context/03-services.md, 05-data-model.md. Depends on F-013.

---
DISCOVER 2026-08-23 verified every premise above against the code. All five held. Three things nobody had recorded turned up, and one changed the feature's shape:
- ReportingService would report ZEROS FOREVER: revenue and completed counts derive from AppointmentStatus, and nothing in production ever set anything but Requested. Wiring it as-is ships a dashboard that looks like a business fact and is structurally a bug. So server-owned appointment status is IN SCOPE for F-014, not a separate feature.
- Appointment status was client-asserted and unguarded: Book()/Complete() were dead code while UpdateAppointmentCommandHandler:51 copied the client's value.
- Cancellation refused a BOOKED appointment — latent, and activated by fixing the above, so both are fixed together.
- Revenue cannot be computed at all: AppointmentEntity does not record which service an appointment is for. The report says so rather than publishing a wrong number (ADR-039).

SPLIT: slot correctness (Start<End, future-dating, overlap) moved to F-025 `booking-correctness` (agenda-buddy-ohw). It is a different shape of work needing its own concurrency design, and has no technical dependency on this feature.

---
SHIPPED 2026-08-23 as v0.4.0. Merged b760794 (PR #40). Episode 004: docs/pdlc/episodes/EPISODE_wire-unreached-services_2026-08-23.md. 701 tests (452+175+74), 19/19 ACs, ADR-036...039. Verified against a live AppHost. Claim released. The roadmap's reason for bundling it was thematic, not technical.
