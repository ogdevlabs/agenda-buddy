---
id: F-027
title: carter-route-modules
status: shipped
priority: 27
labels: [roadmap, "priority:27"]
claimed_by: null
created: 2026-08-27
updated: 2026-08-27
---
Filed 2026-08-27, user-suggested mid-F-020, evaluated and deliberately deferred rather than bundled in. Carter (CarterCommunity/Carter, 2.4k star, active) is a thin ASP.NET Core Minimal API extension: each route group becomes an ICarterModule class (AddRoutes(IEndpointRouteBuilder)), auto-registered via AddCarter()/MapCarter() -- directly addresses "organize routes out of Program.cs" for all 7 services' now-large Program.cs files. Orthogonal to CQRS/MediatR/FluentResults/DataResponse<T> -- it only reorganizes route registration, not dispatch or response shape, so it has no dependency on F-019/F-020's work.

Why not bundled into F-020: F-020 was already 2x its original scope and mid-Construction when this was raised -- retrofitting then meant either redoing in-flight work or a disjointed partial adoption. Better done as its own feature after F-020 ships, when all 7 services share the same Program.cs-with-inline-routes shape, so Carter modules land uniformly in one pass.

Also worth deciding: Carter ships its own FluentValidation integration (Validate<T>) -- evaluate whether that competes with or complements the already-adopted Validot (ADR-049), rather than assume.

Source: docs/pdlc/memory/ROADMAP.md F-027 row.
