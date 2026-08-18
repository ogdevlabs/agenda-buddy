# 15 — CQRS Kernel and Messaging

**Files:** `EventAndCommands/` (commands, queries, handlers, events, `Persitency/`), the six per-service `Requests/RequestCollection.cs` + `Requests/IRequestCollection.cs`, the six `Events/Events?Helper.cs`.

This is the architecture `CONSTITUTION.md` §3 describes as *"CQRS via MediatR: commands and queries are separated in `EventAndCommands`; handlers consume Library domain services; command handlers persist success/failure events to EventStore."* The **structure** matches that description. The **mechanism** does not: MediatR's dispatcher is never used.

---

## ⚠️ Finding 1 — MediatR is registered but never dispatches

`AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly))` is called in six services (`Booking/Program.cs:9`, Calendar `:13`, Customer `:12`, Provider `:13`, Services `:13`, Profession `:11`) and Identity (`:16`).

**Grep for `.Send(` across all `*.cs`: zero matches.** No command or query is ever dispatched through `IMediator`. Instead, each service's `RequestCollection` **manually constructs the handler and calls `.Handle()` directly**:

`Booking/Requests/RequestCollection.cs:10-15`:
```csharp
var result = await new BookingAppointmentCommandHandler(
        mediator, kafkaClient as KafkaClient, providerService, bookingService,
        appointmentEntity, eventStore)
    .Handle(new BookAppointmentCommand { AppointmentEntity = appointmentEntity },
            new CancellationToken());
```

The same shape appears at `RequestCollection.cs:23` (update), `:33` (cancel), and in `Provider/Requests/RequestCollection.cs:10` (add), `:28` (update), `:44` (get all), `:53` (get by email). **Inference:** Calendar, Customer, Services, and Profession follow identically — their `RequestCollection`/`EventsHelper` pairs were not read line-by-line in this scan.

**Why it is built this way:** every handler takes **domain data as primary-constructor parameters**, not just services:

| Handler | Non-service ctor parameters | Anchor |
|---|---|---|
| `BookingAppointmentCommandHandler` | `AppointmentEntity appointmentEntity` | `:9` |
| `CancelAppointmentCommandHandler` | `string appointmentIdentifier` | `:9` |
| `UpdateAppointmentCommandHandler` | `AppointmentEntity appointmentEntity` | `:9` |
| `AddCustomerCommandHandler` | `CustomerEntity customerEntity` | `:7` |
| `UpdateCustomerCommandHandler` | `string email`, `CustomerEntity customerEntity` | `:4,7` |
| `AddProviderCommandHandler` | `ProviderEntity providerEntity` | `:7` |
| `UpdateProviderCommandHandler` | `string email`, `ProviderEntity providerEntity` | `:4,7` |
| `AddServicesToProviderCommandHandler` | `List<ServiceEntity>`, `string email` | `:6,7` |
| `GetProviderByEmailQueryHandler` | `string email` | *(via `RequestCollection.cs:53`)* |

MediatR resolves handlers from the DI container, and `AppointmentEntity` / `string email` / `List<ServiceEntity>` are not registered services — so **`mediator.Send()` could not construct these handlers even if it were called**. The hand-construction is not an oversight; it is a necessary consequence of putting request data in the constructor instead of the request object.

**Consequences:**
- The `TRequest` parameter passed to `Handle` is **largely ignored** — `BookingAppointmentCommandHandler.Handle:14` never reads `request`, using the constructor's `appointmentEntity` instead. `UpdateProviderCommandHandler:16` reads `request.ProviderEntity` for the notification but `providerEntity` (ctor) for the actual write. `UpdateCustomerCommandHandler:13` publishes `request.CustomerEntity` but persists `customerEntity` (ctor) — **two different objects**, so the notification and the write can disagree.
- **No MediatR pipeline behaviours are possible** — no `IPipelineBehavior` for validation, logging, transactions, or retry. The extension seam CQRS-via-MediatR exists to provide is unavailable.
- `IRequest<T>` on every command/query is **decorative** — the interface is implemented and never used for dispatch.
- The `MediatR` 12.3.0 package reference in 8 projects buys only `mediator.Publish` (see finding 2), which is also a no-op.

⚠️ **`kafkaClient as KafkaClient`** (`Booking/Requests/RequestCollection.cs:11,23,33`) casts the injected `IKafkaClient` down to the concrete class, because handlers declare `KafkaClient?` rather than `IKafkaClient`. If a different `IKafkaClient` implementation were ever registered (a test double, a null-object), the cast yields `null` silently. `Provider/Requests/RequestCollection.cs:12` uses `(kafkaClient as KafkaClient)!` — a null-forgiving operator on a cast that can genuinely be null, so `AddProviderCommandHandler` would `NullReferenceException` at `:54`. The abstraction is registered and then defeated at the point of use.

⚠️ **`new CancellationToken()`** is passed at every call site (`RequestCollection.cs:14,26,35,45,54`) — a default, never-cancelled token. The HTTP request's `CancellationToken` is available in every endpoint and is never threaded through, so client disconnects do not abort work.

---

## ⚠️ Finding 2 — every `mediator.Publish` is a no-op

`EventAndCommands/Events/` holds **19 `INotification` classes** across 6 domains:

| Domain | Events |
|---|---|
| Booking | `BookAppointmentEvent`, `CancelAppointmentEvent`, `UpdateAppointmentEvent` |
| Calendar | `CheckCalendarAppointmentsEvent`, `CheckCalendarAvailabilityEvent` |
| Customer | `AddCustomerEvent`, `GetAllCustomersEvent`, `GetCustomerByEmailEvent`, `UpdateCustomerEvent` |
| Profession | `AddProfessionEvent`, `GetProfessionByNameEvent`, `GetProfessionsEvent` |
| Provider | `AddProviderEvent`, `DeactivateProviderEvent`, `GetAllProvidersEvent`, `GetProviderByEmailEvent`, `UpdateProviderEvent` |
| Services | `AddServicesToProviderEvent`, `GetServicesFromProviderEvent`, `UpdateServicesFromProviderEvent` |

All are property-only DTOs. Representative — `EventAndCommands/Events/Booking/BookAppointmentEvent.cs`:
```csharp
[ExcludeFromCodeCoverage]
public class BookAppointmentEvent : INotification
{
    public AppointmentEntity? AppointmentEntity { get; set; }
}
```

Every handler publishes one as its first act — `BookingAppointmentCommandHandler.cs:16`, `CancelAppointmentCommandHandler.cs:15`, `UpdateAppointmentCommandHandler.cs:15`, `AddCustomerCommandHandler.cs:14`, `AddProviderCommandHandler.cs:17`, `AddProfessionCommandHandler.cs:14`, `AddServicesToProviderCommandHandler.cs:14`, `GetProvidersQueryHandler.cs:11`, and so on.

**Grep for `INotificationHandler` across all `*.cs`: zero matches.**

There is **not one notification handler in the solution**. Every `mediator.Publish(...)` awaits an empty handler set and returns. The "event-driven" layer publishes into a void.

⚠️ `DeactivateProviderCommandHandler.cs:11` compounds this — it publishes **the command itself** (`await mediator.Publish(request, ...)`) rather than the `DeactivateProviderEvent` that exists for the purpose. `DeactivateProviderEvent` is defined and never instantiated. Since `DeactivateProviderCommand : IRequest<string>` and not `INotification`, this only compiles because `Publish` accepts `object` — and at runtime it finds no handlers and silently does nothing.

⚠️ 19 event classes, all `[ExcludeFromCodeCoverage]`, all inert. `CLAUDE.md` and `CONSTITUTION.md` §3 present this as an event-driven architecture; mechanically it is a synchronous call chain with dead publish calls interleaved.

---

## The actual request flow

What `CONSTITUTION.md` §3 implies:
```
endpoint → IMediator.Send(command) → [pipeline] → handler → Library service → repository
                                          ↘ IMediator.Publish(event) → notification handlers
```

What the code does:
```
endpoint
  → EventsHelper.<Verb>Event(...)          // static pass-through, zero logic
    → IRequestCollection.<Verb>Request(...) // manually news up the handler
      → new <X>CommandHandler(...).Handle(...)
        ├─ mediator.Publish(event)          // ⚠️ no handlers — no-op
        ├─ Library service → IRepository<T> → MongoDB
        └─ eventStore.SaveAsync(Event)      // audit document
```

### ⚠️ `EventsHelper` is a pure pass-through layer

`Booking/Events/EventsHelper.cs` — three static methods, each forwarding to `IRequestCollection` and returning the result unchanged:

```csharp
public static async Task<string> BookAppointmentEvent(IRequestCollection requestCollection, IMediator mediator,
    ProviderService providerService, BookingService bookingService, AppointmentEntity appointmentEntity)
{
    var notificationResponse =
        await requestCollection.BookAppointmentRequest(mediator, providerService, bookingService, appointmentEntity);
    return notificationResponse;                                          // :8-11
}
```

`Provider/Events/EventsHelper.cs:5-41` is the same for four operations. The class adds **no validation, no mapping, no error handling, no logging** — it renames the call and forwards every argument. The endpoint must supply `IRequestCollection`, `IMediator`, `ProviderService`, `BookingService`, and the entity, so the indirection also forces every endpoint to inject five dependencies it only passes along (`Booking/Program.cs:95-98`).

⚠️ The naming is actively misleading: `EventsHelper.BookAppointmentEvent` returns a `string`, publishes nothing, and shares its name with the `BookAppointmentEvent` **notification class** — two different types of thing with one name, in projects that reference each other.

⚠️ Six copies (`Booking/Events/EventsHelper.cs`, `Calendar/Events/EventHelper.cs`, `Customer/Events/EventsHelper.cs`, `Provider/Events/EventsHelper.cs`, `Services/Events/EventHelper.cs`, `Profession/Events/EventsHelper.cs`) — and the class name itself is inconsistent: `EventsHelper` in Booking/Customer/Provider/Profession, `EventHelper` in Calendar/Services.

---

## Command inventory

11 commands, 11 handlers, in `EventAndCommands/Commands/`.

| Command | Returns | Handler | Reachable from |
|---|---|---|---|
| `BookAppointmentCommand` | `string` | `BookingAppointmentCommandHandler` | `POST api/v1/booking/appointments` |
| `UpdateAppointmentCommand` | `string` | `UpdateAppointmentCommandHandler` | `PUT api/v1/booking/appointments/` |
| `CancelAppointmentCommand` | `string` | `CancelAppointmentCommandHandler` | `DELETE api/v1/booking/appointments/` |
| `BookCalendarCommand` | `bool` | ⚠️ `BookCalendarCommandHandler` — **`throw new NotImplementedException()`** (`:7`) | nothing |
| `AddCustomerCommand` | `string` | `AddCustomerCommandHandler` | `POST api/v1/customers` |
| `UpdateCustomerCommand` | `string` | `UpdateCustomerCommandHandler` | `PUT api/v1/customers/{email}` |
| `AddProviderCommand` | `string` | `AddProviderCommandHandler` | `POST api/v1/providers` |
| `UpdateProviderCommand` | `string` | `UpdateProviderCommandHandler` | `PUT api/v1/providers/{email}` |
| `DeactivateProviderCommand` | `string` | `DeactivateProviderCommandHandler` | ⚠️ **no route** |
| `AddServicesToProviderCommand` | `ProviderEntity` | `AddServicesToProviderCommandHandler` | `PUT api/v1/services/{email}` |
| `UpdateServicesFromProviderCommand` | `ProviderEntity` | `UpdateServicesFromProviderCommandHandler` | `PATCH api/v1/services/{email}` |

⚠️ **`DeactivateProviderCommand` has a complete handler, a unit test (`EventsAndCommands.Tests/Commands/Provider/DeactivateProviderCommandHandlerTest.cs`), and no HTTP route.** Provider deactivation (the soft-delete path, `:30`) is unreachable. Meanwhile `ProviderService.DeleteProviderAsync` (hard delete) is also unreachable. There is **no way to remove a provider** through the API.

⚠️ **`AddProviderCommand.TopicName` is the only property on the command** (`:6`) and the handler never reads it — it recomputes the topic from `providerEntity.Email` (`:53`). `Provider/Requests/RequestCollection.cs:17` populates it from `providerEntity.KafkaTopic!`, which is **null at that point** (it is assigned later, at handler `:20`). A null-forgiving operator on a value that is null, feeding a property nobody reads.

## Query inventory

10 queries, 10 handlers, in `EventAndCommands/Queries/`. Read in full: `GetProvidersQueryHandler`. **Inference:** the other nine follow the same publish → query → audit shape.

| Query | Returns | Reachable from |
|---|---|---|
| `GetProvidersQuery` | `List<ProviderEntity>` | `GET api/v1/providers` |
| `GetProviderByEmailQuery` | `ProviderEntity` | `GET api/v1/providers/{email}` |
| `GetCustomersQuery` | `List<CustomerEntity>` | `GET api/v1/customers` |
| `GetCustomerByEmailQuery` | `CustomerEntity` | `GET api/v1/customers/{email}` |
| `GetProfessionsQuery` | `List<ProfessionEntity>` | `GET api/v1/professions` |
| `GetProfessionByNameQuery` | `ProfessionEntity` | `GET api/v1/professions/{name}` |
| `GetServicesFromProviderQuery` | `List<ServiceEntity>` | `GET api/v1/services/{email}` |
| `CheckCalendarAvailabilityQuery` | `List<DateTime>` | `GET api/v1/calendar/availability/{email}` |
| `CheckCalendarAppointmentsQuery` | `List<AppointmentEntity>` | `GET api/v1/calendar/appointments/{email}` |

⚠️ **There is no read-model separation.** Both sides of the "CQRS" read and write the **same** Mongo collections through the **same** `IRepository<T>` and the **same** `Library` services. No projections, no denormalised read store, no eventual consistency. The separation is a namespace convention (`Commands/` vs `Queries/`), not an architectural one — which is a legitimate design choice, but it means the CQRS label describes folder layout rather than a segregated model.

---

## The EventStore audit trail

`EventAndCommands/Persitency/` — note the directory name is a known typo for "Persistency", flagged in `CLAUDE.md` and `CONSTITUTION.md` §9 as not-to-be-renamed until a dedicated refactor.

`IEventStore` (`IEventStore.cs`): `SaveAsync(Event)` and `GetEventsAsync(ObjectId aggregateId)`.
Registered `Scoped` by `AddEventStore()` (`EventAndCommands/ServiceCollectionExtensions.cs:9`) in all six domain services; **not** in Identity.

`EventStore` (`EventStore.cs:7-12`):
```csharp
public EventStore(IConfiguration configuration)
{
    var client = new MongoClient(configuration.GetSection("MongoDB")["ConnectionString"]);
    var database = client.GetDatabase(configuration.GetSection("MongoDB")["DatabaseName"]);
    _eventCollection = database.GetCollection<Event>(configuration.GetSection("MongoDB")["EventsCollection"]);
}
```

⚠️ **Violates the repository-pattern constraint.** `CONSTITUTION.md` §2 forbids direct MongoDB access outside `MongoDbRepository<T>`; this constructs its own `MongoClient`, `IMongoDatabase`, and `IMongoCollection<Event>` (`04-data-access.md`).

⚠️ **A new `MongoClient` per scope.** `EventStore` is `Scoped`, so **every HTTP request that touches a handler creates a fresh `MongoClient`** — each of which owns a connection pool and background monitoring threads. The driver's explicit guidance is one client per process. This is the most significant resource leak in the codebase.

⚠️ **Reads the root-level `MongoDB` section**, which exists only in `appsettings.Development.json` — the same defect that makes the whole backend Development-only (`06-configuration.md`).

### Every handler writes success/fail events

The pattern, from `BookingAppointmentCommandHandler.cs:20-40`:
```csharp
var successEvent = new Event {
    Id = ObjectId.GenerateNewId(), TimeStamp = DateTime.UtcNow,
    Status = "Success", Type = "BookAppointmentCommand",
    Data = JsonSerializer.Serialize(appointmentEntity) };
await eventStore.SaveAsync(successEvent);
```
...with a mirrored `Status = "Failed"` block on the failure path. Repeated in all 11 command handlers and all 10 query handlers — **42 near-identical blocks**. `CONSTITUTION.md` §3 mandates this ("do not remove this pattern").

⚠️ **Queries write audit events too.** `GetProvidersQueryHandler.cs:17-25` serialises the **entire provider list** — every provider, with embedded appointments and customer emails — into a Mongo document on every call. That endpoint is **anonymous** (`13-security.md`), so an unauthenticated caller can force unbounded PII writes into the audit collection at will. This is simultaneously a write-amplification problem, a PII-retention problem, and an unauthenticated-write amplification vector.

⚠️ **No actor, no correlation, no request id.** `Event` has `timestamp`, `status`, `type`, `data` and nothing else (`05-data-model.md`). The audit trail cannot answer "who did this", and cannot be joined to the `requestId` returned to clients (`10-error-handling.md`).

⚠️ **`GetEventsAsync` cannot work.** `EventStore.cs:21` filters `Builders<Event>.Filter.Eq(e => e.Id, aggregateId)` — but `Id` is the **event's own primary key**, freshly generated per event (`:22` in each handler), not an aggregate identifier. So the method can only ever return the single event whose `_id` matches, never an aggregate's stream. There is no `AggregateId` field on `Event`. **The event-sourcing read path is structurally non-functional** — latent, since nothing calls it.

⚠️ **Audit writes are not transactional with the domain write.** `SaveAsync` is a separate, non-transactional `InsertOneAsync` after the domain mutation. A crash in between produces a mutation with no audit record, or the reverse. No transactions exist anywhere in the solution (`03-services.md`).

⚠️ **`Data` is a JSON string inside a BSON document** — doubly encoded, unqueryable without `$regex` over serialised text, and unindexed. There is no retention or pruning policy; the `events` collection grows without bound.

⚠️ **No test for `EventStore`** (`11-testing.md`).

---

## ⚠️ Finding 3 — Kafka carries no messages

Detailed in `09-integrations.md`. The CQRS-relevant summary:

- `IKafkaClient` declares exactly one method, `CreateTopicIfNotExist` (`Kafka/IKafkaClient.cs:5`). There is **no producer and no consumer** in the solution.
- Topics are created during provider and customer registration (`AddProviderCommandHandler.cs:51-55`, `AddCustomerCommandHandler.cs:47-51`) and then never written to or read from.
- `BootstrapServers` is hardcoded to `localhost:9092` (`KafkaClient.cs:12`) — `CONSTITUTION.md` §9 records this as an outstanding blocker for non-local deployment.
- Topic names discard the email domain, so `sarah@gmail.com` and `sarah@outlook.com` collide on `provider-sarah-topic` (`KafkaHelper.cs:17`) — the "per-provider topic" convention in `CONSTITUTION.md` §3 is per-email-localpart in practice.
- Three command handlers declare `KafkaClient? kafkaClient` and **never use it**, suppressed with `#pragma warning disable CS9113 // Primary constructor parameter unused — kafkaClient reserved for future Kafka publishing` (`BookingAppointmentCommandHandler.cs:1`, `CancelAppointmentCommandHandler.cs:1`, `UpdateAppointmentCommandHandler.cs:1`). The comment is honest: the parameter is aspirational.
- A topic that **already exists** is reported as failure (`KafkaClient.cs:35-36` returns `"Exception …already exists."`), so re-registering a previously-deleted provider's email returns HTTP 400.
- Kafka is therefore a **hard, blocking dependency of user registration** (up to 10 s `RequestTimeout`, `:28`) for a subsystem that transports nothing.

**Net effect:** F-007 `provider-customer-messaging` shipped as "In-app messaging… using the existing Kafka infrastructure". `MessageService` (`Library/Services/MessageService.cs`) implements messaging entirely over **MongoDB**, is not registered in any service collection, and has no HTTP route (`03-services.md`). Kafka is not used by messaging, and messaging is not reachable.

---

## Control-flow and error signalling

⚠️ **Failure is signalled by `null!` and magic strings, not exceptions.**

| Handler | Failure return | Anchor |
|---|---|---|
| `BookingAppointmentCommandHandler` | `null!` | `:41` |
| `UpdateAppointmentCommandHandler` | `null!` | `:40` |
| `CancelAppointmentCommandHandler` | `null!` | `:46` |
| `DeactivateProviderCommandHandler` | `null!` | `:27` |
| `AddServicesToProviderCommandHandler` | `null!` | `:52` |
| `AddProviderCommandHandler` | the Kafka error **string** | `:45` |
| `AddCustomerCommandHandler` | the Kafka error **string** | `:42` |
| `UpdateProviderCommandHandler` | `string.Empty` | `:51` |
| `UpdateCustomerCommandHandler` | `string.Empty` | `:62` |

Endpoints then branch on `!string.IsNullOrEmpty(x) && !x.ToLower().StartsWith("exception")` (`Booking/Program.cs:110`, `Customer/Program.cs:114`, `Provider/Program.cs:121`). Three different failure encodings — `null`, `""`, and `"exception…"` — for one concept, with no type-system support. See `10-error-handling.md`.

⚠️ **Success returns `entity.ToJson()`** — the MongoDB **BSON** serialiser (`BookingAppointmentCommandHandler.cs:29`, `UpdateProviderCommandHandler.cs:35`, `DeactivateProviderCommandHandler.cs:42`), producing extended-JSON with `$oid`/`$date` wrappers. The endpoints discard the string and return the entity object instead (`Booking/Program.cs:111`), so the serialisation cost is paid and thrown away — and the two serialisers (`System.Text.Json` for audit `Data`, BSON `ToJson` for return values) disagree on representation.

⚠️ **`CancelAppointmentCommandHandler` refuses to cancel booked or completed appointments.** `:66-67`:
```csharp
if (appointment.AppointmentStatus == AppointmentStatus.Booked) return false;
if (appointment.AppointmentStatus == AppointmentStatus.Completed) return false;
```
Only `Requested` appointments can be cancelled. F-004 shipped "book, confirm, update, **cancel**, complete" as a lifecycle, and the mobile UI offers Cancel on confirmed appointments (`STATE.md`: *"Cancel/Complete use ActionSheet"*). **Failure scenario:** a provider taps Cancel on a booked appointment → handler returns false → `DELETE` returns 400 "Cancel Appointment Error". The core cancel path is broken for the only status a real appointment has.

⚠️ **`SearchAppointmentAsync` is called three times per cancel** — `Handle:17`, `SearchAndCancelAppointment:51`, `CancelAppointment:65` — three separate Mongo round-trips for the same document, with no null check at `:52` on the second (`04-data-access.md`).

⚠️ **`BookingAppointmentCommandHandler.SearchAndUpdateProviderAppointments:51-54`** inserts the appointment, then **re-reads it from Mongo** (`bookingService.SearchAppointmentAsync`) to append to the provider's embedded list, then replaces the whole provider document. Three writes/reads and a read-modify-write on an unbounded array with no concurrency control — two simultaneous bookings for one provider lose one appointment (`05-data-model.md`).

---

## Summary: architecture as described vs as built

| `CONSTITUTION.md` §3 / `CLAUDE.md` claim | Reality |
|---|---|
| "CQRS via MediatR" | ⚠️ Commands/queries separated by folder; **MediatR dispatch never used**, handlers hand-constructed |
| "handlers consume Library domain services" | ✅ True |
| "command handlers persist success/failure events to EventStore" | ✅ True — and **query handlers do too**, including full PII payloads |
| "Event sourcing (audit trail) … do not remove this pattern" | ⚠️ Audit-log only. `GetEventsAsync` cannot read a stream; there is no replay, no aggregate id, no actor |
| "Kafka provides async provider-to-customer messaging via per-provider topics" | ⚠️ Topics are created and never used. No producer, no consumer. Topic names collide across email domains |
| "Event-driven microservices" | ⚠️ 19 `INotification` types, **zero `INotificationHandler`s**. Services never call each other; they share one database |

The gap is not that the code is wrong about its own intent — the `#pragma` comment at `BookingAppointmentCommandHandler.cs:1` ("*reserved for future Kafka publishing*") is candid. The gap is that the memory-bank documents describe the intended end state as the current state.
