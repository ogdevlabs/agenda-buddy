# 10 — Error Handling

**Files:** the `UseExceptionHandler` block duplicated in all 7 `Program.cs`; `<Service>/Extensions/ProblemDetailsServiceEndpointFilter.cs` (×6); `<Service>/Extensions/HttpContextExtensions.cs` (×7); `Identity/Services/IdentityService.cs:232-235` (exception types); `Library.ServerAuth/Tools/OwnershipGuard.cs:28-37`.

The response envelope is **RFC 7807 ProblemDetails**. There is no central middleware class and no `IExceptionHandler` implementation — the handler is an inline lambda copy-pasted into every service.

---

## ⚠️ The central finding: no exception handler in production

In every service the exception handler is registered **inside the `IsDevelopment()` guard**, together with Swagger:

```csharp
if (app.Environment.IsDevelopment())      // Booking/Program.cs:38
{
    app.UseSwagger();                     // :40
    app.UseSwaggerUI();                   // :41
    app.UseExceptionHandler(new ExceptionHandlerOptions { ... });   // :43-79
}
```

| Service | Guard line | `UseExceptionHandler` line |
|---|---|---|
| Booking | `:38` | `:43` |
| Calendar | `:38` | `:43` |
| Customer | `:38` | `:43` |
| Provider | `:42` | `:47` |
| Services | `:39` | `:44` |
| Profession | `:38` | `:43` |
| Identity | `:40` | `:44` |

**Failure scenario:** run any service with `ASPNETCORE_ENVIRONMENT=Production`. An unhandled exception has **no** handler registered, so Kestrel returns a bare `500` with an empty body — no ProblemDetails, no `requestId`, nothing correlatable. The `requestId` correlation mechanism described below exists **only in Development**.

This inverts the usual ASP.NET convention (`UseDeveloperExceptionPage` in Development, `UseExceptionHandler` in Production). Note it is partially moot today because the six domain services cannot start outside Development at all (`06-configuration.md`) — but Identity **can**, and Identity is therefore the one service that runs in production with no exception handler.

---

## The handler body (identical in all 7 services)

`Booking/Program.cs:43-79` — reproduced structurally:

```csharp
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    AllowStatusCode404Response = true,                                  // :45
    ExceptionHandler = async exceptionContext =>
    {
        var feature = exceptionContext.Features.Get<IExceptionHandlerFeature>();   // :49

        if (feature?.Error is BadHttpRequestException badRequestEx)                // :51
            exceptionContext.Response.StatusCode = badRequestEx.StatusCode;        // :52

        if (exceptionContext.Request.AcceptsJson()                                 // :54
            && exceptionContext.RequestServices
                 .GetRequiredService<IProblemDetailsService>() is { } svc)         // :55-56
        {
            await svc.WriteAsync(new ProblemDetailsContext                          // :59
            {
                HttpContext = exceptionContext,
                AdditionalMetadata = feature?.Endpoint?.Metadata,                   // :62
                ProblemDetails = { Status = exceptionContext.Response.StatusCode }  // :63
            });
        }
        else                                                                        // :66
        {
            exceptionContext.Response.ContentType = "text/plain";                   // :68
            var message = ReasonPhrases.GetReasonPhrase(
                exceptionContext.Response.StatusCode) switch
            {
                { Length: > 0 } reasonPhrase => reasonPhrase,                       // :71
                _ => "An error occurred"                                            // :72
            };
            await exceptionContext.Response.WriteAsync(message + "\r\n");            // :74
            await exceptionContext.Response.WriteAsync(
                $"Request ID: {Activity.Current?.Id ?? exceptionContext.TraceIdentifier}");  // :75-76
        }
    }
});
```

`:48` carries a comment linking `dotnet/aspnetcore#43831` — the framework issue tracking first-class support for this pattern.

### Handler-to-status map

| Condition | Status | Body | Anchor |
|---|---|---|---|
| `BadHttpRequestException` (malformed body, bad route value) | the exception's own `StatusCode` (typically 400) | ProblemDetails or text | `:51-52` |
| Any other exception, `Accept: application/json` | **500** (`Response.StatusCode` unchanged) | ProblemDetails JSON with `requestId` | `:54-65` |
| Any other exception, non-JSON `Accept` | 500 | `text/plain` — reason phrase + `Request ID: <id>` | `:66-77` |

⚠️ **Only `BadHttpRequestException` is mapped.** Every domain exception falls through to 500:

| Exception | Thrown at | Surfaces as |
|---|---|---|
| `ForbiddenException` | `OwnershipGuard.cs:11,18,24` | ⚠️ **500** — unless caught inline (see below) |
| `ArgumentException("Provider not found")` | `ProviderService.cs:23` | ⚠️ 500 (should be 404) |
| `ArgumentException("Customer Not Found")` | `CustomerService.cs:18` | ⚠️ 500 (should be 404) |
| `KeyNotFoundException` | `NoteService.cs:33,47`; `PaymentService.cs:34`; `ReportingService.cs:11` | ⚠️ 500 (should be 404) |
| `UnauthorizedAccessException` | `NoteService.cs:36,50` | ⚠️ 500 (should be 403) |
| `InvalidOperationException` | `AppointmentEntity.cs:56,64`; `PaymentService.cs:37,40` | ⚠️ 500 (should be 409/400) |
| `NotImplementedException` | `ServiceService.cs:7,12`; `BookCalendarCommandHandler.cs:7` | ⚠️ 500 |
| `FormatException` | `MongoDbRepository.cs:28` (`new ObjectId(id)` on a bad id) | ⚠️ **500 for what is a client input error** |
| `MongoException` / `TimeoutException` | driver | ⚠️ 500 in the six domain services; **503** in Identity only |
| `ApplicationException` (JWT key missing) | `AuthenticationExtensions.cs:19`; `IdentityService.cs:190` | Startup crash / 500 |

⚠️ **`ForbiddenException` carries a `StatusCode => 403` property (`OwnershipGuard.cs:30`) that nothing ever reads.** The exception handler does not inspect it; only `BadHttpRequestException.StatusCode` is honoured (`:52`). The property is decorative. Forbidden results work only because every guarded endpoint wraps the call in a local `try/catch`:

```csharp
try { OwnershipGuard.AssertOwner(user, email); }
catch (ForbiddenException) { return TypedResults.Forbid(); }
```
— `Booking/Program.cs:104,128,153`; `Customer/Program.cs:133`; `Provider/Program.cs:182`; `Services/Program.cs:122,146`.

**This is repeated at 8 call sites.** Any new guarded endpoint that forgets the `try/catch` returns **500 instead of 403** — a silent security-signalling regression with no compile-time protection. Mapping `ForbiddenException → 403` in the exception handler (or an `IExceptionHandler`) would remove the footgun.

⚠️ **`FormatException` from `new ObjectId(id)`** (`MongoDbRepository.cs:28`) is the most likely live 500: any client passing a non-24-hex-character id to a path that reaches `GetByIdAsync` gets a 500 rather than 400/404.

---

## `AddProblemDetails` and the `requestId` extension

Registered in all seven services with a customisation callback:

```csharp
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        CustomizeProblemDetails(context.ProblemDetails, context.HttpContext));   // Booking/Program.cs:21-22

void CustomizeProblemDetails(ProblemDetails problemDetails, HttpContext httpContext)
{
    problemDetails.Extensions["requestId"] =
        Activity.Current?.Id ?? httpContext.TraceIdentifier;                     // :171-174
}
```

The identical local function appears at `Booking/Program.cs:171`, `Calendar/Program.cs:146`, `Customer/Program.cs:177`, `Provider/Program.cs:199`, `Services/Program.cs:164`, `Profession/Program.cs:157`, `Identity/Program.cs:174`.

`Activity.Current?.Id` is the W3C trace-id when a distributed-tracing context exists. ⚠️ **No tracing is configured** (`12-observability.md`), so `Activity.Current` is populated only by ASP.NET's built-in activity source and the id **is not exported anywhere** — there is no log sink, no trace backend, and no correlation across services. The `requestId` is returned to the client but cannot be looked up. Compounded by the fact that services never call each other, so there is no cross-service trace to correlate anyway.

⚠️ **`GenerateErrorMessage` is duplicated as a local function in five services** — `Booking/Program.cs:176`, `Customer/Program.cs:182`, `Provider/Program.cs:204`, `Profession/Program.cs:162`, `Services/Program.cs` *(absent — Services has no `GenerateErrorMessage`)*, `Calendar/Program.cs` *(absent)*. It wraps a `string key` + `string[] values` into a `Dictionary<string, string[]>` for `TypedResults.ValidationProblem`. Five identical copies; a `Library` helper would suffice.

---

## `ProblemDetailsServiceEndpointFilter`

`Booking/Extensions/ProblemDetailsServiceEndpointFilter.cs` — an `IEndpointFilter` applied to the route group in six services (`Booking/Program.cs:91`, `Calendar:91`, `Customer:91`, `Provider:96`, `Services:92`, `Profession:91`). ⚠️ **Identity does not apply it** — the only service without it.

Purpose: route-handler results that are `ProblemHttpResult` or a bare `ProblemDetails` are re-wrapped in a private `ProblemDetailsServiceAwareResult` (`:23`) so that `IProblemDetailsService` — and therefore the `requestId` customisation — is applied to them. Without this filter, `TypedResults.Problem(...)` would bypass `CustomizeProblemDetails`.

```csharp
return await next(context) switch
{
    ProblemHttpResult r => new ProblemDetailsServiceAwareResult(r.StatusCode, r.ProblemDetails),  // :15-16
    ProblemDetails pd   => new ProblemDetailsServiceAwareResult(null, pd),                        // :17
    { } result          => result,                                                                // :18
    null                => null                                                                  // :19
};
```

⚠️ **`ExecuteAsync` silently does nothing if `IProblemDetailsService` is unresolvable** (`:35` uses `GetService`, not `GetRequiredService`, with no `else`). If `AddProblemDetails` were ever removed, the response would be a `200` with an empty body rather than an error. All six services do register it, so this is latent.

⚠️ **`ValidationProblem` results are not intercepted** — the switch matches only `ProblemHttpResult` and `ProblemDetails`. `TypedResults.ValidationProblem(...)` returns `ValidationProblem`, which falls through `{ } result => result` at `:18`. Since validation failures are the **most common** error path in this API (every mutating endpoint starts with `MiniValidator.TryValidate`), the majority of 400 responses **do not carry a `requestId`**. The filter's stated purpose is largely unmet in practice.

⚠️ The class is duplicated verbatim across six projects with only the namespace differing, and each is marked `[ExcludeFromCodeCoverage]` (`:8`) — so this logic is untested six times over.

---

## `HttpContextExtensions.AcceptsJson`

`Booking/Extensions/HttpContextExtensions.cs` (duplicated in all 7 services). Three overloads (`:13`, `:24`, `:35`) resolving to:

```csharp
if (httpRequest.GetTypedHeaders().Accept is { Count: > 0 } acceptHeader)
    return acceptHeader.Any(v => mediaType.IsSubsetOf(v));    // :37-38
return false;                                                  // :40
```

⚠️ **Returns `false` when no `Accept` header is present** (`:40`), so a client that omits `Accept` — which many HTTP clients and `curl` do by default — receives the **`text/plain`** error branch (`Program.cs:66-77`) rather than ProblemDetails JSON. The pragmatic default for an API is to treat a missing `Accept` as `*/*` and prefer JSON. This is the only place in the error pipeline with well-formed XML doc comments (`:8-12`, `:18-23`, `:29-34`).

⚠️ Also `[ExcludeFromCodeCoverage]` (`:3`) — but `Booking.Tests`, `Calendar.Tests` etc. do not test it either way.

---

## Identity's parallel scheme

Identity does not use the shared pattern. It defines four exception types at `Identity/Services/IdentityService.cs:232-235`:

```csharp
public class AuthValidationException(string message)  : Exception(message);
public class ConflictException(string message)        : Exception(message);
public class UnauthorizedException(string m = "Invalid credentials.") : Exception(m);
public class ServiceUnavailableException()            : Exception("Authentication service temporarily unavailable.");
```

and catches them **inline per route** in `Identity/Program.cs`:

| Route | Catches | Maps to | Anchor |
|---|---|---|---|
| `/register` | `AuthValidationException` | `400 { error: "validation_error", message }` | `:113` |
| | `ConflictException` | `409 { error: "conflict", message }` | `:114` |
| | `ServiceUnavailableException` | `503` ProblemDetails `title: "service_unavailable"` | `:115` |
| `/login` | `UnauthorizedException` | `401` (empty body) | `:125` |
| | `ServiceUnavailableException` | `503` | `:126` |
| `/refresh` | `UnauthorizedException` | `401` | `:138` |
| | `ServiceUnavailableException` | `503` | `:139` |
| `/logout` | `ServiceUnavailableException` | `503` | `:151` |

⚠️ **Two incompatible error envelopes coexist in the same product.** The six domain services return RFC 7807 (`type`/`title`/`status`/`detail`/`requestId`); Identity returns an ad-hoc `{ error, message }` object for 400/409 and RFC 7807 only for 503. A client must handle both shapes. `MobileApp` handles neither — it only checks `response.IsSuccessStatusCode` (`MobileApp/Services/AuthService.cs:33`, `BookingApiService.cs:27`) and discards the body entirely, so no error message ever reaches the user (`16-mobile-client.md`).

⚠️ **`ConflictException` and `UnauthorizedException` share their names with nothing else, but `ForbiddenException` lives in `Library.ServerAuth/Tools/OwnershipGuard.cs:28`** — so the solution's exception types are split across two assemblies with no common base type or marker interface.

⚠️ **`AuthValidationException` is thrown but partly unreachable.** `IdentityService.RegisterAsync:29-33` validates password length and role, but `Identity/Program.cs:103-106` performs the *same* validation before calling the service. The service-layer checks are dead on the HTTP path (correct defensive duplication, but the `catch` at `:113` can only fire for inputs the route already rejected).

⚠️ **`/logout` returns `204` even for an unknown refresh token** (`Identity/Program.cs:145` and `IdentityService.LogoutAsync:174`) — deliberately idempotent, correctly avoiding token-existence disclosure.

---

## Where each error originates

| Layer | Style | Reaches the client as |
|---|---|---|
| Route handler (validation) | `MiniValidator.TryValidate` → `TypedResults.ValidationProblem` | 400, ⚠️ **no `requestId`** (filter gap above) |
| Route handler (authz) | `try/catch (ForbiddenException)` → `TypedResults.Forbid()` | 403 — only where the `try/catch` was written |
| Route handler (not found) | `TypedResults.NotFound()` / `NoContent()` | 404 / 204 |
| Command/query handler | ⚠️ **magic strings** — `"Exception: …"` returned as the result value, sniffed with `.StartsWith("exception")` | 400 with a hand-built `GenerateErrorMessage` dictionary |
| `Library` services | Real exceptions (`ArgumentException`, `KeyNotFoundException`, `UnauthorizedAccessException`) | ⚠️ **500** — unmapped |
| `KafkaClient` | ⚠️ magic strings, never throws | 400 "Kafka Error" |
| `CacheAside` | ⚠️ returns `default!` on lock timeout — **no error at all** | ⚠️ **spurious 404/204** (`04-data-access.md`) |
| Repository | Driver exceptions propagate | 500 (503 in Identity) |
| `IdentityService` | Typed exceptions | 400/401/409/503 |

⚠️ **The string-sentinel convention is the weakest link.** `Booking/Program.cs:110`, `:134`, `:159`; `Customer/Program.cs:114`; `Provider/Program.cs:121`; and `AddProviderCommandHandler.cs:18,34`, `AddCustomerCommandHandler.cs:16,31` all branch on whether a returned string starts with `"exception"`. Handlers also return `null!` for failure (`BookingAppointmentCommandHandler.cs:41`, `UpdateAppointmentCommandHandler.cs:40`, `CancelAppointmentCommandHandler.cs:46`, `DeactivateProviderCommandHandler.cs:27`) which the endpoints test with `!string.IsNullOrEmpty(...)`. There is no `Result<T>` type and no exception — failure information is a string prefix.

---

## Operational notes

- ⚠️ **`UseStatusCodePages()`** is called in all seven services (`Booking/Program.cs:85`) but **after** `UseExceptionHandler` is conditionally registered. It supplies a minimal text body for bare status codes with no body — which is what makes production 500s non-empty at all.
- ⚠️ **No logging of exceptions.** The handler writes a response and returns; it never calls `ILogger`. ASP.NET's `ExceptionHandlerMiddleware` does log at `Error` level by default, but nothing in this codebase adds context (user, route, correlation id), and there is no log sink (`12-observability.md`). **An unhandled exception in production leaves no durable record.**
- ⚠️ **No `ProblemDetails.Type` URI** is ever set — every error omits the `type` member that RFC 7807 uses for machine-readable classification.
- ⚠️ **No `detail` on domain errors** — `ProblemDetails = { Status = ... }` (`:63`) sets only the status; `title` is inferred from the status code and `detail` is left null. Error responses are therefore near-contentless.
- ⚠️ **Stack traces are never exposed** — correct, and the only security-positive aspect of the handler being Development-gated is moot since the handler also does not leak in Development.

## What is missing

- No `IExceptionHandler` implementation (the .NET 8+ idiomatic replacement for the lambda).
- No exception-to-status mapping table; no `ForbiddenException → 403`, `KeyNotFoundException → 404`, `InvalidOperationException → 409`.
- No production exception handler at all.
- No `Result<T>`/discriminated-union error channel — the codebase uses string sentinels and `null!`.
- No retry, circuit breaker, or timeout policy on any outbound call (`09-integrations.md`).
- No error-rate alerting, no dead-letter path, no dead-letter queue for the Kafka topics that exist.
- No tests for any error path in the six domain services — every error-handling class is `[ExcludeFromCodeCoverage]` (`11-testing.md`).
