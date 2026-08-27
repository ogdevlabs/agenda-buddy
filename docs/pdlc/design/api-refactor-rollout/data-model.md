# Data Model — API Refactor Rollout (F-020)

**No changes.** This feature rewrites Calendar's, Customer's, Provider's, Services's, and Profession's
endpoint/handler layering and dispatch mechanism — it does not touch any `Library/Entities/*.cs` file, any
`[BsonElement]` mapping, any MongoDB collection schema, or any `Library.Services.*` persistence behavior.
Each entity continues to be persisted exactly as it is today. Unlike F-019's original (never-delivered)
plan, this feature does not introduce Mapster-based request/response DTOs — PRD Out of Scope explicitly
declines repeating Booking's own Requirement 7, which shipped with zero call sites. Entities keep flowing
through route signatures unchanged, same as Booking does today.
