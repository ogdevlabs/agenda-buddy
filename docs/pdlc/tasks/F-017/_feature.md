---
id: F-017
title: container-and-cd-hardening
status: shipped
priority: 18
labels: [roadmap, "priority:18"]
claimed_by: null
created: 2026-08-15
updated: 2026-08-26
---
Fix the container and CI story. (1) Library/Dockerfile:13, Kafka/Dockerfile:13, and EventAndCommands/Dockerfile:12 publish net10.0 output onto a dotnet/runtime:8.0 base - these images cannot run; F-011 missed them and CI builds no images so nothing caught it. (2) Delete the three class-library Dockerfiles and their Compose services (events, kafka-library, common-library have no ENTRYPOINT). (3) Add the CONSTITUTION section 7 MANDATORY security scan - dependency audit plus secret scan - which is currently unimplemented despite being marked un-uncheckable; a secret scanner would have caught the committed Atlas credential. (4) Add image build, scan, and push to CI. (5) MOVED TO F-018 on 2026-08-18: the integration-test capability. F-018 builds it with Testcontainers (real MongoDB/Kafka per run) rather than bare WebApplicationFactory, because the API refactor program needs it as a safety net before any endpoint is rewritten. CONSTITUTION section 5's 'all integration tests pass' becomes satisfiable there, not here. Source: docs/pdlc/context/08-cicd-deploy.md, 11-testing.md.
