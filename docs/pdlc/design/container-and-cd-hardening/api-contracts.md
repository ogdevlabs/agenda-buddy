# API Contracts: container-and-cd-hardening (F-017)
<!-- pdlc-template-version: design-doc -->

No new API endpoints. This feature is CI/build-infrastructure-only — it adds no HTTP route to any of the seven ASP.NET Minimal API services or the Gateway, and modifies no existing endpoint's request/response shape, authentication requirement, or status codes. The two new CI jobs (`security-scan`, `docker-build-and-scan`) run entirely within GitHub Actions and have no runtime HTTP surface of their own to document.
