# Data Model — API Refactor Pilot: Booking (F-019)

**No changes.** This feature rewrites Booking's endpoint/handler layering and dispatch mechanism — it does
not touch `Library/Entities/AppointmentEntity.cs`, any `[BsonElement]` mapping, any MongoDB collection
schema, or `Library.Services.BookingService`'s persistence behavior. `AppointmentEntity` continues to be
persisted exactly as it is today; the only change is that it stops being the *public API contract* (PRD
Requirement 7) — new request/response DTOs in `Booking.Domain` sit between the wire and the entity, mapped
by `Mapster`.
