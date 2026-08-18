---
id: F-014
title: wire-unreached-services
status: planned
priority: 16
labels: [roadmap, "priority:16"]
claimed_by: null
created: 2026-08-15
updated: 2026-08-18
---
Register and route the six shipped-but-unreachable capabilities: NotificationService, MessageService, NoteService, PaymentService, ReportingService, and DeactivateProviderCommand. All six have domain implementations and unit tests but no DI registration, no configured collection, and no HTTP route - so F-006 through F-010 are marked Shipped while being unreachable. Also needs collection names in appsettings (no NotificationsCollection/MessagesCollection/NotesCollection/PaymentsCollection key exists) and a DI-registerable IPaymentGateway (StripePaymentGateway takes a raw string apiKey with no Stripe config section anywhere). Source: docs/pdlc/context/03-services.md, 05-data-model.md. Depends on F-013.
