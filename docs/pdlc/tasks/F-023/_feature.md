---
id: F-023
title: token-revocation
status: shipped
priority: 23
labels: [roadmap, "priority:23"]
claimed_by: oscargarcia@ogdevlabs.onmicrosoft.com
created: 2026-08-18
updated: 2026-08-27
---
There is no token revocation. A fresh jti GUID is minted into every access token (IdentityService.cs:204) but it is never recorded and never checked, and no denylist exists. LogoutAsync:176 clears the stored refresh token, but the ACCESS token stays valid for up to 60 minutes after the user logs out. Combined with ValidateAudience = false (AuthenticationExtensions.cs:40) - no aud claim is issued, so all seven services accept any token this issuer minted - a leaked or post-logout token has the widest possible blast radius.

Filed rather than absorbed into the Platform Remediation program at Discover 2026-08-18 because it is not a one-task fix. It requires a real design decision: where the denylist lives (the shared IDistributedCache is per-process AddDistributedMemoryCache today, so it cannot back a cross-service denylist - see 00-overview.md finding 7), what the per-request check costs on every authenticated route across seven services, and how entries expire. Also worth deciding alongside: whether to introduce an aud claim and turn ValidateAudience back on, which narrows blast radius without a denylist.

Related: F-021 (identity-hardening) fixes the auth system's other defects but deliberately leaves this one, because this needs design and those did not.

Source: docs/pdlc/context/13-security.md:71,77, docs/pdlc/brainstorm/brainstorm_platform-remediation_2026-08-18.md.
