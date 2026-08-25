---
id: F-021
title: identity-hardening
status: shipped
priority: 15
labels: [roadmap, "priority:15"]
claimed_by: null
created: 2026-08-18
updated: 2026-08-25
---
Second in the Platform Remediation program. Split out of F-016 at Discover 2026-08-18 because F-016 plus the absorbed defects plus the verification harness grew past one PRD. Where F-016 closes endpoint exposure, this fixes the auth system's own defects.

1. RefreshAsync delete-then-insert PERMANENTLY DESTROYS ACCOUNTS. Identity/Services/IdentityService.cs:135 calls FindOneAndDeleteAsync on the whole CredentialEntity and :155 re-inserts it. Any exception or process termination between those lines - including the IsMongoDown catch at :157-160 firing on the insert - loses the user's email, password hash and role irrecoverably. No audit trail (Identity does not use the EventStore) and no logging. The atomic delete IS a correct single-use-token guard; the bug is that it deletes the whole document instead of the embedded refresh_token subdocument. Correct fix is a targeted $set/$unset or FindOneAndUpdate. Underlying cause: IRepository<T> offers no partial-update primitive. Untestable by the current suite - InMemoryRepository cannot simulate a mid-operation fault (11-testing.md:65).
2. UseHttpsRedirection is registered AFTER UseAuthentication/UseAuthorization in 6 services (Booking/Program.cs:83-86 and equivalents). The bearer token is parsed and validated from the plaintext HTTP request before the redirect is issued, so the credential has already crossed the insecure channel. Redirection must precede authentication.
3. No rate limiting and no account lockout. AddRateLimiter appears nowhere in the solution; POST /api/v1/auth/login accepts unlimited attempts. The T-005 timing mitigation defends against enumeration but nothing defends against credential stuffing or brute force.
4. [OUT OF SCOPE — ALREADY FIXED BY F-016, T09/AC-21, regression tests at OwnershipGuardTest.cs:116,128. Excluded because it is merged, not because it is deferred.] AssertOwner passes on a null claim. Library.ServerAuth/Tools/OwnershipGuard.cs:9-10 - if the NameIdentifier claim is null and entityEmail is also null, string.Equals(null, null) is true and the guard SUCCEEDS. AssertOwnerAny explicitly checks for this first; AssertOwner does not. One-line fix.

Claim: the auth system itself is safe.

WARNING WITHDRAWN AT DEFINE - the premise was false. No test in F-016's harness calls POST /api/v1/auth/login: TokenFactory mints JWTs locally (TokenFactory.cs:39,85-86). Verified again at Construction. The limiter still ships switched OFF by default and switchable ON by the harness, which is what AC-15 needs - but that is threat T-103's mitigation, not a workaround for a collision that never existed.

Source: docs/pdlc/context/13-security.md, docs/pdlc/brainstorm/brainstorm_platform-remediation_2026-08-18.md.
