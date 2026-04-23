# Design: Day 5 Documentation — .NET 10 E-Commerce Showcase

**Date:** 2026-04-23  
**Branch:** master  
**Status:** Approved  
**Approach:** Layered narrative (B)

---

## Problem Statement

Day 5 of the 5-day execution plan is documentation-only. The goal is to produce a documentation set that passes a senior .NET engineer's 10-minute technical screen. Every doc has one job: help an interviewer understand why these patterns were chosen, not just that they were used.

No new code. The gate (CI green + `docker compose up` working) was met at end of Day 4.

---

## Deliverables

| File | Status | Description |
|------|--------|-------------|
| `README.md` | Update (moderate) | Replace thin architecture section with "why" narrative |
| `docs/architecture.md` | Create | 5-paragraph portfolio narrative + ASCII diagram + layer boundaries |
| `docs/adr/001-clean-architecture.md` | Create | Nygard ADR |
| `docs/adr/002-cqrs-meditr.md` | Create | Nygard ADR |
| `docs/adr/003-ddd-aggregates.md` | Create | Nygard ADR |
| `docs/adr/004-no-microservices.md` | Create | Nygard ADR |
| `docs/adr/005-clean-arch-vs-vsa.md` | Create | Nygard ADR |

---

## Approach: Layered Narrative

Docs form a reading funnel:

```
README.md          → hook, quick start, "why" summary, links out
docs/architecture.md → full narrative, ASCII diagram, layer boundaries
docs/adr/00X.md    → deep decision records per architectural choice
```

An interviewer who reads only the README gets the key signals. One who reads `architecture.md` gets the full story. One who checks an ADR sees structured engineering judgment. No content is repeated across levels — each doc has one job.

---

## Section 1: README.md Changes

### What stays

- Title + CI badge
- Quick start with `docker compose up` in first screenful
- API overview table
- Local development section
- Running tests section
- Tech stack table
- Project structure

### What changes

Replace lines 33–45 (current thin architecture section) with a **"Why These Patterns"** section:

**Why Clean Architecture**  
One paragraph: the dependency rule means the domain compiles with zero infrastructure dependencies. Business logic is testable without mocking EF Core or ASP.NET Identity. Adding a new database or replacing JWT with OAuth touches only the Infrastructure layer.

**Why CQRS + MediatR**  
One paragraph: e-commerce reads (product listing, order history) vastly outnumber writes. MediatR pipeline behaviors (ExceptionHandling → Logging → Validation) apply uniformly to every command and query without scattered try/catch or duplicated middleware.

**Why DDD Aggregates**  
One paragraph: Cart, Order, and Product each have invariants that HTTP handlers could violate if entities were plain POCOs. Domain methods (`Cart.AddItem`, `Order.Cancel`) enforce rules at the point of mutation — invalid state is not representable.

**Footer links:**
```
→ Full narrative: [docs/architecture.md](docs/architecture.md)  
→ Decision records: [docs/adr/](docs/adr/)
```

---

## Section 2: `docs/architecture.md`

### Paragraph structure (portfolio narrative tone — readable by senior interviewer in ~3 min)

1. **What Clean Architecture solves** — the dependency rule: Domain → Application → Infrastructure → API, never reversed. Domain has zero NuGet dependencies. Business logic doesn't know databases or HTTP exist. Infrastructure is a plugin.

2. **Why for e-commerce specifically** — catalog queries, cart mutations, and order placement have different consistency requirements. CA lets you vary the read side (projections, caching) without touching aggregate invariants. The domain boundary is the unit of testability and the unit of change.

3. **Where CQRS fits** — reads return DTOs directly from EF Core projections; writes go through aggregate methods that enforce invariants then commit. The MediatR pipeline (ExceptionHandling → Logging → Validation) applies to every command and query without repetition. Handlers are focused: one handler, one use case.

4. **DDD aggregate boundaries** — `Product` owns pricing and activation rules; `Cart` owns item-level invariants (no duplicates, positive quantities, price snapshots at add-time); `Order` owns its lifecycle (Placed → Cancelled). Aggregate boundaries = transaction boundaries. No cross-aggregate references in a single command.

5. **Conscious trade-offs** — monolith over microservices: no distributed transactions, `docker compose up` is one command, CI is simple. CQRS without event sourcing: there is no audit log requirement and eventual consistency adds complexity for no current benefit. These are deliberate choices, not omissions.

### ASCII diagram

```
┌─────────────────────────────────────────────────┐
│                   API Layer                     │
│   Minimal APIs · JWT auth · Rate limiting       │
│   Maps HTTP → MediatR dispatches                │
├─────────────────────────────────────────────────┤
│              Application Layer                  │
│   Commands · Queries · Handlers · Behaviors     │
│   MediatR pipeline: ExceptionHandling →         │
│   Logging → Validation → Handler                │
├─────────────────────────────────────────────────┤
│               Domain Layer                      │
│   Aggregates · Value Objects · Domain Events    │
│   Repository interfaces · No NuGet deps         │
├─────────────────────────────────────────────────┤
│            Infrastructure Layer                 │
│   EF Core 10 · PostgreSQL · ASP.NET Identity    │
│   JWT generation · Migrations · Seeding         │
└─────────────────────────────────────────────────┘

Dependencies flow inward only. Infrastructure
references Domain + Application; never the reverse.
```

### Per-layer boundary section (4 paragraphs)

**Domain** — contains aggregates (`Product`, `Cart`, `Order`), value objects (`Money`, `UserId`, `CartItem`), domain exceptions, and repository/unit-of-work interfaces. Zero NuGet dependencies. This layer defines what the system does; it does not care how it is stored, tested, or exposed over HTTP. Everything else depends on Domain; Domain depends on nothing.

**Application** — contains all use cases as MediatR commands and queries, FluentValidation validators, pipeline behaviors, and service interfaces (`IJwtTokenGenerator`, `IUserService`). Depends on Domain only. This is where orchestration lives: fetch aggregate, call domain method, commit, return DTO. No EF Core, no HTTP primitives.

**Infrastructure** — implements every interface defined in Domain and Application. Contains `AppDbContext`, EF entity configurations, repository implementations, `AuditInterceptor`, `UserService`, and `JwtTokenGenerator`. Depends on Domain and Application. Swapping PostgreSQL for another database touches only this layer.

**API** — the composition root. Minimal API endpoint groups map HTTP verbs to MediatR dispatches. `Program.cs` wires all layers together via `AddApplication()` and `AddInfrastructure()`. No business logic lives here. The API layer is intentionally thin: if a handler grows beyond dispatch + response mapping, it has leaked logic that belongs in Application.

---

## Section 3: ADR Format and Content

### Format (Nygard classic)

```markdown
# ADR-00X: Title

## Status
Accepted

## Context
...

## Decision
...

## Consequences
...
```

~300–400 words per ADR. Each ADR answers one specific "why not X" question. Target read time: 2 minutes.

---

### ADR 001 — Clean Architecture

**Status:** Accepted  
**Context:** Portfolio API targeting senior .NET hiring screens. Domain logic must be demonstrably testable without spinning up a database. Flat-folder or MVC-style structure would interleave business rules with EF and HTTP concerns.  
**Decision:** 4-layer Clean Architecture with strict inward-only dependency rule. Domain has zero NuGet packages.  
**Consequences:** (+) Domain tests require no test doubles for infrastructure. (+) Adding or replacing infrastructure (database, auth provider) is bounded to one layer. (−) More boilerplate than a flat project structure. (−) Simple CRUD features require creating a command, handler, and validator even when there are no real invariants to protect.

---

### ADR 002 — CQRS via MediatR

**Status:** Accepted  
**Context:** E-commerce reads (product listing, cart retrieval, order history) vastly outnumber writes. Cross-cutting concerns (structured logging, input validation, exception mapping to ProblemDetails) must apply uniformly without duplicating try/catch in every handler.  
**Decision:** MediatR 12 for command/query dispatch. Three pipeline behaviors registered in order: `ExceptionHandlingBehavior` → `LoggingBehavior` → `ValidationBehavior`.  
**Consequences:** (+) Pipeline behaviors compose cleanly — adding a new cross-cutting concern is one class registration. (+) Commands and queries are self-documenting records; the use-case list is the handler list. (−) MediatR is runtime indirection — stack traces require familiarity with the library to read. (−) Can feel like over-engineering for CRUD endpoints with no real invariants.

---

### ADR 003 — DDD Aggregates

**Status:** Accepted  
**Context:** Cart, Order, and Product each have invariants (cart items must have positive quantity; orders can only be cancelled when in Placed status; products must have a valid Money value object). Modeling these as plain POCOs would allow HTTP handlers to create invalid state.  
**Decision:** Model each as a DDD aggregate with factory methods and domain methods that enforce invariants. EF Core annotations are banned from domain entities — all mapping lives in `Infrastructure/Persistence/Configurations/`.  
**Consequences:** (+) Invalid aggregate state is not representable via the public API. (+) Business rules are co-located with the data they protect. (−) Steeper onboarding than an anemic CRUD model. (−) EF configuration is more verbose when using owned entities and value converters.

---

### ADR 004 — No Microservices

**Status:** Accepted  
**Context:** Single developer, 5-day build, portfolio project. Microservices require distributed tracing, a service mesh, inter-service authentication, and distributed transaction strategies (saga or 2PC) — none of which are relevant to this scope.  
**Decision:** Single deployable monolith. One Docker container for the API, one for PostgreSQL. `docker compose up` is the entire deployment.  
**Consequences:** (+) Zero-setup demo: one command, no manual steps. (+) No distributed transaction complexity — cart checkout and order placement are a single database transaction. (+) CI is a single build and test step. (−) Would require bounded-context extraction and service decomposition if the team or transaction volume grew significantly. This is a known, accepted constraint for the current scope.

---

### ADR 005 — Clean Architecture vs Vertical Slice Architecture

**Status:** Accepted  
**Context:** Vertical Slice Architecture (VSA) groups all code for a feature in one folder — handler, validator, endpoint, and any DTOs together. It is increasingly popular in the .NET community (Jimmy Bogard, FastEndpoints) and avoids the cross-layer navigation that CA requires.  
**Decision:** Use Clean Architecture. The primary portfolio signal here is demonstrable domain/infrastructure separation and DDD aggregate modeling — signals that VSA deliberately de-emphasizes by grouping by feature rather than by layer.  
**Consequences:** (+) Layer separation is immediately legible to any senior .NET interviewer without explanation. (+) Domain layer isolation is explicit and verifiable (check the `.csproj` — no EF dependency). (−) Feature code is spread across layers: handler in Application, entity in Domain, EF configuration in Infrastructure — navigation requires knowing the layer convention. VSA would keep all of this in one folder, which is genuinely better for large teams working feature-by-feature.

---

## Success Criteria

- `README.md`: "why these patterns" narrative replaces thin architecture section; `docker compose up` visible in first screenful (unchanged); links to `docs/architecture.md` and `docs/adr/` present.
- `docs/architecture.md`: 5 paragraphs present; ASCII diagram present; 4 per-layer boundary paragraphs present; readable by senior .NET interviewer in ~3 minutes.
- Each ADR: Nygard sections present; answers one specific "why not X" question; ~300–400 words; no placeholders.
- All files committed on `master`.

---

## Out of Scope

- New code of any kind (Day 4 gate)
- Redis caching (stretch goal, deferred)
- Railway deployment (optional post-Day 5 stretch)
- Updating CI workflow (already green)
