# F-019-T02 — Validot vertical-slice spike findings

Real end-to-end swap of `MiniValidator.TryValidate` → Validot for exactly one route: `POST
/appointments` in `Booking/Program.cs`. `PUT /appointments/`, `DELETE /appointments/`, and all 7
F-014 routes are untouched.

**Validot version:** `2.6.0` (the only version on nuget.org — confirmed via `dotnet package search
validot`; there is no separate DI-extension package, e.g. no `Validot.DependencyInjection` or
`AddValidot(...)` anywhere in the assembly).

## API shape found (confirmed against the installed assembly, not guessed)

- `Specification<T>` is a delegate: `ISpecificationOut<T> Specification<T>(ISpecificationIn<T> api)`.
  Written as a lambda: `Specification<Foo> spec = s => s.Member(m => m.X, m => ...).And().Member(...)`.
- No DI extension ships with the package. Registration is a plain
  `services.AddSingleton<IValidator<T>>(Validator.Factory.Create(spec))` — Validot validators are
  immutable/stateless, so singleton is the correct lifetime (same effective lifetime
  `MiniValidator.TryValidate`'s static call already had).
- A result is `IValidationResult`: `bool AnyErrors`, and
  `IReadOnlyDictionary<string, IReadOnlyList<string>> MessageMap` (path → messages). Mapped to the
  route's existing `TypedResults.ValidationProblem(IDictionary<string,string[]>)` shape via
  `validationResult.MessageMap.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray())` — response
  contract unchanged, confirmed by test (`EmailProvider`/`EmailCustomer` keys match MiniValidator's
  member-name keys exactly).
- `EmailRules.Email(this IRuleIn<T>, EmailValidationMode mode = ComplexRegex)` has a
  `EmailValidationMode.DataAnnotationsCompatible` mode that delegates to the same
  `System.ComponentModel.DataAnnotations.EmailAddressAttribute` logic MiniValidator already uses via
  `[EmailAddress]`. Using that mode means the two validation paths cannot disagree on what a valid
  email looks like — this was the deciding factor over the regex mode.

## The `.Required()` trap — confirmed real, but not the fix this field needed

Bolt's roundtable claim was verified directly against the installed package:

| Spec | `""` | `null` |
|---|---|---|
| `.Required()` alone | **valid** (no error) | invalid |
| `.Required().NotEmpty()` | invalid | invalid |
| `.Optional().Email(DataAnnotationsCompatible)` | invalid | **valid** (no error) |

The trap is real: `.Required()` alone treats empty string as valid. But the fix isn't
`.Required().NotEmpty()` — that would *reject null*, and `EmailAddressAttribute.IsValid(null) ==
true` today (confirmed directly against the live `.NET` implementation, not assumed), so `null` must
stay valid to match current behavior exactly. `AppointmentEntity` has no `[Required]` today. The
correct match is `.Optional().Email(EmailValidationMode.DataAnnotationsCompatible)`:
`.Optional()` reproduces "null is fine", and `.Email(...)` intrinsically rejects `""` and any
malformed string, without a separate `.NotEmpty()`.

## Diff list

### `AppointmentEntity` (wired into `POST /appointments` — the one real vertical slice)

| Field | MiniValidator/annotation today | Validot rule authored | Notes |
|---|---|---|---|
| `EmailProvider` | `[EmailAddress]` only, no `[Required]`. `null` valid, `""` invalid, malformed invalid, well-formed valid. | `.Optional().Email(EmailValidationMode.DataAnnotationsCompatible)` | Byte-for-byte behavioral match, confirmed by test (`AppointmentEntitySpecificationTest`). |
| `EmailCustomer` | Same as above. | Same as above. | Same as above. |
| `Identifier`, `Start`, `End`, `AppointmentStatus`, `AppointmentDescription`, `DayOff` | No annotations, no MiniValidator rule. | No rule authored. | Not touched — out of scope, matches today exactly (nothing to port). |

### `AppointmentStatusRequest(string Status)` — authored only, **not wired**

| Field | MiniValidator today | Validot rule authored | Notes |
|---|---|---|---|
| `Status` | Zero validation. No enum-membership check (validated downstream, not by MiniValidator). | **No rule authored** — spec is `s => s` (pass-through, touches no member). | **Not ported — no validation exists today.** Adding an enum-membership rule here would be new, tightened behavior beyond current, which the roundtable explicitly ruled out. |

### `NoteRequest(string Content)` — authored only, **not wired**

| Field | MiniValidator today | Validot rule authored | Notes |
|---|---|---|---|
| `Content` | Zero validation. | `.Required().NotEmpty()` — rejects `null` and `""`. | **New behavior, not ported.** Authored deliberately (rather than a no-op) to demonstrate a real rule chain per the task's "author reasonable specs" instruction. Confirmed by test that it is NOT wired into any live route in this task — wiring is T05/T06's job. |

### `PaymentRequest(decimal Amount, string? Currency)` — authored only, **not wired**

| Field | MiniValidator today | Validot rule authored | Notes |
|---|---|---|---|
| `Amount` | Zero validation. No positivity check. | **No rule authored** — spec is `s => s`. | **Not ported — new behavior, out of scope.** Per the roundtable instruction, adding a positivity check here is explicitly deferred; there is no price to validate `Amount` against (see `Amount`'s own doc comment on `PaymentRequest`, threat T-205). |
| `Currency` | Zero validation. Nullable (`string?`). | **No rule authored.** | Per the roundtable instruction, `.Required()` must not be added to a nullable field — omitted entirely rather than risk a subtler equivalent (e.g. `.Optional()` with a hidden format rule). |

## Test coverage

- `Booking.Tests/Validation/AppointmentEntitySpecificationTest.cs` — 3 tests, written and run failing
  *before* `AppointmentEntitySpecification` existed (compile-error red), then made to pass. Covers:
  malformed provider email still errors, empty customer email still errors (the `""` case
  `.Required()` alone would have missed), and both-emails-well-formed passes with no errors.
- `Booking.Tests/Validation/AppointmentExtrasRequestsSpecificationsTest.cs` — 5 tests, same
  red-first discipline. Covers: `StatusSpec` accepts any string (no validation exists), `NoteSpec`
  rejects empty content and accepts non-empty content, `PaymentSpec` accepts a negative amount and a
  null currency (both intentionally unvalidated).

## Files changed/created

- `Booking/Booking.csproj` — added `Validot` `2.6.0` package reference.
- `Booking.Tests/Booking.Tests.csproj` — added `Validot` `2.6.0` package reference (tests construct
  the validator directly, the same way `Program.cs` registers it).
- `Booking/GlobalUsings.cs` — added `global using Booking.Validation;` and `global using Validot;`.
- `Booking/Validation/AppointmentEntitySpecification.cs` — new. The one wired spec.
- `Booking/Validation/AppointmentExtrasRequestsSpecifications.cs` — new. The three authored-only,
  unwired specs.
- `Booking/Program.cs` — registered `IValidator<AppointmentEntity>` as a singleton; swapped
  `MiniValidator.TryValidate` for the injected validator at `POST /appointments` only. `PUT
  /appointments/` and `DELETE /appointments/` still call `MiniValidator.TryValidate` unchanged.
- `Booking.Tests/Validation/AppointmentEntitySpecificationTest.cs` — new.
- `Booking.Tests/Validation/AppointmentExtrasRequestsSpecificationsTest.cs` — new.
