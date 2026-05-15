---
phase: 1
slug: solution-scaffold-domain-model
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-15
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (xunit.v3 3.2.x) via `dotnet test` |
| **Config file** | `backend/Storage.sln` — Wave 0 creates `Storage.Domain.Tests` |
| **Quick run command** | `dotnet test backend/Storage.Domain.Tests/Storage.Domain.Tests.csproj --no-build` |
| **Full suite command** | `dotnet test backend/Storage.Domain.Tests/Storage.Domain.Tests.csproj` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test backend/Storage.Domain.Tests/Storage.Domain.Tests.csproj --no-build`
- **After every plan wave:** Run `dotnet test backend/Storage.Domain.Tests/Storage.Domain.Tests.csproj`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 1-01-01 | 01 | 1 | SETUP-01 | manual | `docker-compose up --build -d` | ❌ W0 | ⬜ pending |
| 1-01-02 | 01 | 1 | SETUP-02 | build | `dotnet build backend/Storage.sln` | ❌ W0 | ⬜ pending |
| 1-01-03 | 01 | 1 | SETUP-03 | build | `dotnet build backend/Storage.sln` | ❌ W0 | ⬜ pending |
| 1-01-04 | 01 | 1 | SETUP-04 | manual | `curl http://localhost:5000/health` | ❌ W0 | ⬜ pending |
| 1-01-05 | 01 | 1 | SETUP-05 | build | `dotnet build backend/Storage.sln --no-incremental` | ❌ W0 | ⬜ pending |
| 1-02-01 | 02 | 2 | DOMAIN-01 | unit | `dotnet test ... --filter "FileEntity"` | ❌ W0 | ⬜ pending |
| 1-02-02 | 02 | 2 | DOMAIN-02 | unit | `dotnet test ... --filter "FileCategory"` | ❌ W0 | ⬜ pending |
| 1-02-03 | 02 | 2 | DOMAIN-03 | unit | `dotnet test ... --filter "StatusTransition"` | ❌ W0 | ⬜ pending |
| 1-02-04 | 02 | 2 | DOMAIN-04 | unit | `dotnet test ... --filter "StorageKey"` | ❌ W0 | ⬜ pending |
| 1-02-05 | 02 | 2 | DOMAIN-05 | unit | `dotnet test ... --filter "Checksum"` | ❌ W0 | ⬜ pending |
| 1-02-06 | 02 | 2 | DOMAIN-06 | unit | `dotnet test ... --filter "DomainEvents"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `backend/Storage.Domain.Tests/Storage.Domain.Tests.csproj` — xUnit v3 test project referencing Storage.Domain only
- [ ] `backend/Storage.Domain.Tests/Domain/FileEntityTests.cs` — stubs for DOMAIN-01, DOMAIN-03
- [ ] `backend/Storage.Domain.Tests/Domain/ValueObjects/StorageKeyTests.cs` — stubs for DOMAIN-04
- [ ] `backend/Storage.Domain.Tests/Domain/ValueObjects/ChecksumTests.cs` — stubs for DOMAIN-05
- [ ] `backend/Storage.Domain.Tests/Domain/Events/DomainEventTests.cs` — stubs for DOMAIN-06

*Wave 0 creates the test project and stub test files before any domain code is written, enabling red-green verification from the first commit.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `docker-compose up` starts all six services | SETUP-01 | Requires Docker daemon; no programmatic check | Run `docker-compose up -d` and verify all 6 containers reach `healthy` status via `docker ps` |
| Ocelot gateway responds on port 5000 | SETUP-04 | Runtime integration; needs running containers | After `docker-compose up`, `curl http://localhost:5000/health` returns 200 |
| ClamAV starts (may take 5–15 min on first boot for definition download) | SETUP-01 | Timing-dependent; definition download is external | Check `docker logs clamav` for "ClamAV daemons are ready" |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
