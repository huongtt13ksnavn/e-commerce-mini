# Day 5 Documentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce the 7 documentation files that complete Day 5 of the 5-day execution plan — README polish, `docs/architecture.md`, and 5 Nygard ADRs — so a senior .NET engineer can evaluate the project in a 10-minute technical screen.

**Architecture:** Layered narrative funnel — README is the hook, `docs/architecture.md` is the full story, ADRs are deep decision records. No content is repeated across levels; each doc has one job.

**Tech Stack:** Markdown only. No new code. All files committed to `master`.

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Modify | `README.md` | Replace thin architecture section with "why" narrative; add links to architecture.md + ADRs |
| Create | `docs/adr/001-clean-architecture.md` | Why Clean Architecture, what it prevents |
| Create | `docs/adr/002-cqrs-meditr.md` | Why CQRS + MediatR, what the pipeline buys |
| Create | `docs/adr/003-ddd-aggregates.md` | Why DDD aggregates, what the boundaries are |
| Create | `docs/adr/004-no-microservices.md` | Why monolith is correct for this scope |
| Create | `docs/adr/005-clean-arch-vs-vsa.md` | Clean Architecture vs Vertical Slice Architecture |
| Create | `docs/architecture.md` | 5-paragraph portfolio narrative + ASCII diagram + layer boundaries |

ADRs are written before `architecture.md` because the architecture doc references them by number.

---

## Task 1: Update README.md — Replace Architecture Section

**Files:**
- Modify: `README.md:33-45`

Replace the current thin architecture section (lines 33–45) with a "Why These Patterns" section that explains the reasoning behind each major choice.

- [ ] **Step 1: Verify current architecture section to replace**

Open `README.md`. The section to replace starts at `## Architecture` and ends before `## Local Development`. It currently reads:

```
## Architecture

4-layer Clean Architecture: Domain → Application → Infrastructure → API.

...

See [`docs/design.md`](docs/design.md) for the full architecture spec...
```

- [ ] **Step 2: Replace the architecture section**

Replace the entire `## Architecture` section with:

```markdown
## Why These Patterns

**Clean Architecture** enforces a strict dependency rule: Domain → Application → Infrastructure → API, never reversed. The Domain layer compiles with zero NuGet dependencies — business logic has no knowledge of databases, HTTP, or ASP.NET. This means the domain is testable without mocking EF Core or ASP.NET Identity, and replacing PostgreSQL with another database touches only the Infrastructure layer.

**CQRS + MediatR** fits e-commerce because reads (product listing, cart retrieval, order history) vastly outnumber writes. Separating commands from queries makes that asymmetry explicit in the codebase. The MediatR pipeline — ExceptionHandling → Logging → Validation — applies cross-cutting concerns uniformly to every command and query without scattering try/catch across handlers.

**DDD Aggregates** prevent invalid state by construction. `Cart`, `Order`, and `Product` each enforce their own invariants through domain methods (`Cart.AddItem`, `Order.Cancel`, `Product.Deactivate`). HTTP handlers cannot bypass these rules — there is no public setter to call. Aggregate boundaries align with transaction boundaries: no command spans more than one aggregate root.

```
┌──────────────┐     ┌─────────────────┐     ┌────────────────────┐
│   Domain     │◄────│   Application   │◄────│   Infrastructure   │
│  (no deps)   │     │  (Domain only)  │     │  (EF Core, JWT)    │
└──────────────┘     └─────────────────┘     └────────────────────┘
                             ▲                         ▲
                             └──────────┬──────────────┘
                                   ┌────┴────┐
                                   │   API   │
                                   └─────────┘
```

→ Full narrative: [docs/architecture.md](docs/architecture.md)
→ Decision records: [docs/adr/](docs/adr/)
```

- [ ] **Step 3: Verify success criteria**

Check that:
- `docker compose up` command is visible in the first screenful (Quick Start section — unchanged)
- "Why These Patterns" section is present with all three subsections (Clean Architecture, CQRS + MediatR, DDD Aggregates)
- Links to `docs/architecture.md` and `docs/adr/` are present at the bottom of the section

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs: replace thin architecture section with why-these-patterns narrative"
```

---

## Task 2: Create docs/adr/001-clean-architecture.md

**Files:**
- Create: `docs/adr/001-clean-architecture.md`

- [ ] **Step 1: Create the adr directory**

```bash
mkdir -p docs/adr
```

- [ ] **Step 2: Write the ADR**

Create `docs/adr/001-clean-architecture.md` with this exact content:

```markdown
# ADR-001: Clean Architecture

## Status

Accepted

## Context

This project targets senior .NET engineering hiring screens. The primary portfolio signals are demonstrable separation of concerns and testable domain logic. A flat-folder or MVC-style structure would interleave business rules with EF Core and ASP.NET concerns, making both harder to read and harder to test independently.

An architecture was needed where domain logic can be compiled, read, and tested in complete isolation from any infrastructure dependency. The architecture also needed to be immediately legible to a senior .NET engineer without explanation.

The alternative considered was a flat structure: a single project with folders for `Models`, `Services`, `Controllers`, and `Data`. This is common in tutorials and entry-level codebases. It works for simple CRUD but makes the boundary between business rules and infrastructure invisible — both live in the same project, both depend on EF Core.

## Decision

Use 4-layer Clean Architecture with a strict inward-only dependency rule:

```
Domain ← Application ← Infrastructure
                     ← API
```

- **Domain** (`ECommerce.Domain`) has zero NuGet package dependencies. Pure C# types only: aggregates, value objects, domain exceptions, and repository/unit-of-work interfaces.
- **Application** (`ECommerce.Application`) depends on Domain only. Contains all use cases as MediatR commands and queries, FluentValidation validators, pipeline behaviors, and service interfaces (`IJwtTokenGenerator`, `IUserService`).
- **Infrastructure** (`ECommerce.Infrastructure`) implements every interface defined in Domain and Application. Contains EF Core, ASP.NET Identity, JWT generation, migrations, and seeding.
- **API** (`ECommerce.API`) is the composition root. Maps HTTP routes to MediatR dispatches. Contains no business logic.

The dependency rule is enforced by `.csproj` references — Infrastructure cannot be imported from Domain because Domain has no reference to Infrastructure. This is compiler-enforced, not convention.

## Consequences

**Positive:**
- Domain tests require no test doubles for infrastructure. `xunit` alone is sufficient to test aggregate invariants and domain logic.
- Adding or replacing infrastructure (database engine, auth provider, caching layer) is bounded to the Infrastructure layer. No domain or application code changes.
- Layer contracts (repository interfaces, service interfaces) are explicit and compiler-enforced via interface definitions in Domain and Application.
- The architecture is a shared vocabulary: any senior .NET engineer can navigate the codebase without a tour.

**Negative:**
- More boilerplate than a flat project structure. A simple CRUD endpoint requires a command record, a handler, a validator, a repository call, and an EF configuration — multiple files where a flat structure would use one.
- Dependency injection wiring is more involved. The composition root (`Program.cs`) is the only place where all layers meet, which means startup errors can be surprising if DI registrations are missing.
```

- [ ] **Step 3: Verify**

Confirm the file has all four Nygard sections: `## Status`, `## Context`, `## Decision`, `## Consequences`. Confirm it answers "why not flat structure".

- [ ] **Step 4: Commit**

```bash
git add docs/adr/001-clean-architecture.md
git commit -m "docs: add ADR-001 clean architecture"
```

---

## Task 3: Create docs/adr/002-cqrs-meditr.md

**Files:**
- Create: `docs/adr/002-cqrs-meditr.md`

- [ ] **Step 1: Write the ADR**

Create `docs/adr/002-cqrs-meditr.md` with this exact content:

```markdown
# ADR-002: CQRS via MediatR

## Status

Accepted

## Context

E-commerce applications have an inherently asymmetric read/write pattern. Reads — product listing, cart retrieval, order history — happen far more frequently than writes. Treating reads and writes identically (same validation pipeline, same repository abstraction) adds overhead where it is not needed and obscures where it is.

Cross-cutting concerns also needed to apply uniformly: every command needs input validation; every operation needs structured logging; every unhandled exception needs to map to a ProblemDetails response rather than a raw 500. Without a mediator, these concerns are either duplicated in each handler or implemented as ASP.NET middleware that cannot intercept application-layer failures.

The alternative considered was service classes (e.g., `IProductService`, `IOrderService`) with direct method calls from endpoints. This is the most common pattern in .NET codebases. It works, but cross-cutting concerns become scattered: each service method ends up with its own try/catch, its own logging call, its own validation invocation.

## Decision

Use MediatR 12 for command/query dispatch. All use cases are `IRequest<TResponse>` records. Commands mutate state; queries return data. No shared base class distinguishes them — convention only.

Three pipeline behaviors are registered in this order (outermost first):

1. `ExceptionHandlingBehavior<TRequest, TResponse>` — catches all unhandled exceptions and maps to ProblemDetails. `DomainException` → 400; `ValidationException` → 422; all others → 500.
2. `LoggingBehavior<TRequest, TResponse>` — logs request name and elapsed milliseconds.
3. `ValidationBehavior<TRequest, TResponse>` — runs all registered FluentValidation validators for the request type; throws `ValidationException` on failure.

Queries bypass `ValidationBehavior` naturally — no validator is registered for them, so the behavior is a no-op.

## Consequences

**Positive:**
- Pipeline behaviors compose cleanly. Adding a new cross-cutting concern (e.g., caching, idempotency) is one `IPipelineBehavior<TRequest, TResponse>` implementation and one DI registration.
- Commands and queries are self-documenting records. The full list of application use cases is the list of `IRequest` types — no service interface scanning needed.
- ExceptionHandling is guaranteed to run for every operation, so no handler can accidentally leak a raw exception to the HTTP response.

**Negative:**
- MediatR is runtime indirection. Stack traces show `MediatR.Mediator.Send` rather than the direct call site, which can confuse engineers unfamiliar with the library.
- Can feel like over-engineering for CRUD endpoints with no real invariants to protect. A `GetProductsQuery` that just calls `repository.GetAllAsync()` is two files (query + handler) where one method call would suffice.
- MediatR is a third-party dependency. If the library is abandoned or breaks API compatibility, all handlers are affected.
```

- [ ] **Step 2: Verify**

Confirm all four Nygard sections present. Confirm it answers "why not service classes".

- [ ] **Step 3: Commit**

```bash
git add docs/adr/002-cqrs-meditr.md
git commit -m "docs: add ADR-002 CQRS via MediatR"
```

---

## Task 4: Create docs/adr/003-ddd-aggregates.md

**Files:**
- Create: `docs/adr/003-ddd-aggregates.md`

- [ ] **Step 1: Write the ADR**

Create `docs/adr/003-ddd-aggregates.md` with this exact content:

```markdown
# ADR-003: DDD Aggregates

## Status

Accepted

## Context

E-commerce domains have natural invariant boundaries that CRUD models cannot express. A cart item must have a positive quantity. A product price must be a valid monetary amount in a known currency. An order cannot be cancelled once it has been completed. These rules exist regardless of which HTTP endpoint is called or which developer writes the handler.

The alternative considered was an anemic domain model: plain C# POCOs with public setters, validation living in the Application layer or in FluentValidation validators. This is common and works — but rules enforced in validators can be bypassed by calling the setter directly. The domain model cannot make invalid state unrepresentable.

A second concern: EF Core annotations on domain entities (`[Key]`, `[Required]`, `[Column]`) create a hidden dependency between the domain model and the ORM. The Domain project should have zero knowledge of how it is persisted.

## Decision

Model `Product`, `Cart`, and `Order` as DDD aggregates:

- **Factory methods only** — no public constructors. `Product.Create(name, price)`, `Cart.Create(userId)` are the only way to construct valid instances.
- **Domain methods enforce invariants** — `Cart.AddItem(productId, quantity, unitPrice)` rejects quantity ≤ 0. `Order.Cancel()` rejects orders not in `Placed` status. The rule lives where the data lives.
- **No EF annotations on domain entities** — all EF mapping lives in `Infrastructure/Persistence/Configurations/{Entity}Configuration.cs` implementing `IEntityTypeConfiguration<T>`. Value objects (`Money`, `CartItem`) are configured as owned entities with value converters for typed IDs (`ProductId`, `OrderId`).
- **Price snapshots** — `CartItem.UnitPrice` is set at add-time from the current product price and never updated. If the product price changes after the item is added, the cart shows the original price. This is intentional e-commerce behavior.

Aggregate boundaries align with transaction boundaries: no command modifies more than one aggregate root in a single `CommitAsync()` call.

## Consequences

**Positive:**
- Invalid aggregate state is not representable via the public API. A handler that skips validation still cannot create a cart with a zero-quantity item.
- Business rules are co-located with the data they protect. Reading `Cart.AddItem` tells you the full invariant — no need to cross-reference a validator.
- Domain layer has zero EF Core dependency. `ECommerce.Domain.csproj` references no ORM package.

**Negative:**
- Steeper onboarding than an anemic CRUD model. Engineers must understand that setters are not the mutation path.
- EF configuration is more verbose: owned entities, value converters for typed IDs, and `HasNoKey()` for read-side projections require explicit configuration that would be implicit with plain `int` IDs and public properties.
- Aggregate method signatures must be stable — changing `Cart.AddItem`'s parameters requires updating all callers, including test fixtures.
```

- [ ] **Step 2: Verify**

Confirm all four Nygard sections present. Confirm it answers "why not anemic model / plain POCOs".

- [ ] **Step 3: Commit**

```bash
git add docs/adr/003-ddd-aggregates.md
git commit -m "docs: add ADR-003 DDD aggregates"
```

---

## Task 5: Create docs/adr/004-no-microservices.md

**Files:**
- Create: `docs/adr/004-no-microservices.md`

- [ ] **Step 1: Write the ADR**

Create `docs/adr/004-no-microservices.md` with this exact content:

```markdown
# ADR-004: No Microservices

## Status

Accepted

## Context

The e-commerce domain (catalog, auth, cart, orders) maps naturally onto four bounded contexts, which is exactly the decomposition a microservices architecture would produce. The question of "why not microservices" is a legitimate one for this domain and is worth answering explicitly for any senior engineer evaluating this codebase.

Microservices offer genuine benefits at scale: independent deployability, independent scalability per bounded context, technology heterogeneity, and fault isolation. They are the right choice when teams are large enough that independent deployment is more valuable than the overhead of distributed systems.

The costs of microservices include: distributed transactions (no single database transaction spans a service boundary — cart checkout touching both Cart and Order services requires a saga or 2PC), inter-service authentication, service discovery, distributed tracing, a service mesh or API gateway, and a CI/CD pipeline per service. These costs are fixed regardless of team size.

## Decision

Deploy as a single monolith. One Docker container for the API, one for PostgreSQL. `docker compose up` is the complete deployment.

The internal architecture (Clean Architecture with bounded domains in `ECommerce.Domain`) preserves the logical separation of Cart, Order, and Product without paying the distributed systems tax. If the system needed to scale to multiple teams or services in the future, the bounded contexts are already delineated — extraction would be a packaging decision, not a redesign.

## Consequences

**Positive:**
- `docker compose up` is the entire deployment. Zero manual steps. Zero infrastructure configuration. Any engineer with Docker Desktop can run the full system in under 90 seconds.
- Cart checkout and order placement are a single database transaction. No distributed transaction strategy needed. No saga. No eventual consistency on the write path.
- CI is a single build and test job. No per-service pipelines to maintain.
- Integration tests use a single `WebApplicationFactory` with Testcontainers — one real PostgreSQL instance, no service stubs.

**Negative:**
- The entire application deploys as one unit. A change to the product catalog requires redeploying the order service.
- A single database means schema migrations affect all bounded contexts simultaneously. At high team count, this creates coordination overhead.
- If any bounded context needs a different scaling profile (e.g., the catalog needs read replicas), the entire application must be replicated rather than just the relevant service.

These are known, accepted constraints for the current scope: single developer, portfolio project, 5-day build.
```

- [ ] **Step 2: Verify**

Confirm all four Nygard sections present. Confirm it answers "why not microservices" directly.

- [ ] **Step 3: Commit**

```bash
git add docs/adr/004-no-microservices.md
git commit -m "docs: add ADR-004 no microservices"
```

---

## Task 6: Create docs/adr/005-clean-arch-vs-vsa.md

**Files:**
- Create: `docs/adr/005-clean-arch-vs-vsa.md`

- [ ] **Step 1: Write the ADR**

Create `docs/adr/005-clean-arch-vs-vsa.md` with this exact content:

```markdown
# ADR-005: Clean Architecture vs Vertical Slice Architecture

## Status

Accepted

## Context

Vertical Slice Architecture (VSA) is an increasingly popular alternative to Clean Architecture in the .NET community, advocated by Jimmy Bogard (creator of MediatR) and implemented by frameworks like FastEndpoints and Carter. VSA organizes code by feature rather than by layer: a `PlaceOrder` feature folder contains the endpoint, handler, validator, command, and any DTOs — all in one place.

VSA makes a compelling argument: code that changes together should live together. When implementing a new feature in Clean Architecture, you touch files in four different projects (Domain, Application, Infrastructure, API). In VSA, you touch one folder. VSA also avoids the overhead of repository interfaces and unit-of-work abstractions for simple features — a handler can use `DbContext` directly.

The question is which architecture better serves this project's specific goal.

## Decision

Use Clean Architecture (layer-based) over Vertical Slice Architecture.

The primary goal of this project is to demonstrate domain/infrastructure separation and DDD aggregate modeling to a senior .NET hiring audience. These are the signals Clean Architecture makes explicit and verifiable:

- The domain compiles with zero infrastructure dependencies (checkable by reading `ECommerce.Domain.csproj`)
- Aggregate invariants are enforced in domain methods, not validators (checkable by reading any aggregate)
- Repository interfaces are defined in Domain, implemented in Infrastructure (checkable by reading the interface and its implementation)

VSA deliberately blurs these boundaries in exchange for feature cohesion. A VSA handler that uses `DbContext` directly is not worse — but it does not demonstrate the same separation signal.

## Consequences

**Positive:**
- Layer separation is immediately legible to any senior .NET interviewer without explanation. The four-project structure is a shared vocabulary.
- Domain layer isolation is explicit and compiler-enforced. The `ECommerce.Domain.csproj` file is the proof.
- The architecture showcases exactly the patterns that senior .NET engineering roles require: Clean Architecture, CQRS, DDD — each demonstrable from the project structure alone.

**Negative:**
- Feature code is spread across layers. Implementing `PlaceOrder` requires touching `ECommerce.Domain` (aggregate methods), `ECommerce.Application` (command + handler + validator), `ECommerce.Infrastructure` (any new EF queries), and `ECommerce.API` (endpoint registration). Navigation requires knowing the layer convention.
- VSA would keep all of this in one folder, which is genuinely better ergonomics for large teams working feature-by-feature. Clean Architecture's cross-layer navigation is a real cost at scale.
- The repository abstraction adds indirection that VSA avoids. For read-only queries with no invariants to protect, `IProductRepository` is ceremony.

For a team of 5+ working on multiple features in parallel, VSA's feature cohesion would likely outweigh Clean Architecture's layer clarity. For this project — single developer, demonstrating architectural judgment — Clean Architecture is the correct choice.
```

- [ ] **Step 2: Verify**

Confirm all four Nygard sections present. Confirm it answers "why not VSA" with genuine tradeoff acknowledgment.

- [ ] **Step 3: Commit**

```bash
git add docs/adr/005-clean-arch-vs-vsa.md
git commit -m "docs: add ADR-005 clean architecture vs VSA"
```

---

## Task 7: Create docs/architecture.md

**Files:**
- Create: `docs/architecture.md`

Written last because it references the ADRs by number.

- [ ] **Step 1: Write the file**

Create `docs/architecture.md` with this exact content:

````markdown
# Architecture

> Portfolio narrative for the .NET 10 E-Commerce Showcase.
> Read time: ~3 minutes.

---

## What Clean Architecture Solves

Clean Architecture is an application of the Dependency Inversion Principle at the structural level. Rather than organizing code by technical role (Models, Services, Controllers), it organizes code by stability: the most stable and most business-critical code — the domain — sits at the center, with no dependencies on anything outside it. Frameworks, databases, and HTTP are details that live at the edges and depend inward. The practical result is a dependency rule: `Domain ← Application ← Infrastructure ← API`, never reversed. In this project that rule is enforced by `.csproj` references. `ECommerce.Domain` has zero NuGet packages. You cannot write an EF Core query in the domain layer because the package is not referenced — the compiler prevents it. This is the key property: the architecture is not a convention that developers must remember to follow; it is a constraint that the build enforces.

## Why for E-Commerce Specifically

E-commerce domains have natural invariants that pure CRUD models cannot protect. A cart item must have a positive quantity. A product price must be a valid monetary amount. An order cannot be cancelled once it has shipped. These rules exist regardless of which endpoint is called or which developer writes the handler. Clean Architecture places these rules in the Domain layer, where they are enforced by domain methods (`Cart.AddItem`, `Order.Cancel`, `Product.Deactivate`) rather than validators that can be bypassed. The domain boundary is also the unit of testability: because `ECommerce.Domain` has no infrastructure dependencies, domain logic can be tested with plain `xunit` — no `WebApplicationFactory`, no Testcontainers, no database. The e-commerce domain has three natural aggregate boundaries (Product, Cart, Order), each with its own consistency requirements and lifecycle, which maps cleanly onto Clean Architecture's aggregate-per-repository pattern. See [ADR-001](adr/001-clean-architecture.md) for the full decision record.

## Where CQRS Fits

E-commerce reads vastly outnumber writes. Product listing, cart retrieval, and order history happen on every page load; placing an order or adding a cart item happens occasionally. Command Query Responsibility Segregation (CQRS) makes this asymmetry explicit: commands mutate state via aggregate methods and commit through a Unit of Work; queries return DTOs projected directly from EF Core without touching domain objects. In this codebase, MediatR 12 is the dispatcher — each command and query is a `IRequest<TResponse>` record, and MediatR routes it to the correct handler. The real value of MediatR here is its pipeline: three behaviors run in order for every operation — `ExceptionHandlingBehavior` catches all unhandled exceptions and maps them to ProblemDetails; `LoggingBehavior` logs request name and elapsed time; `ValidationBehavior` runs FluentValidation validators. These cross-cutting concerns apply uniformly to every command without duplication. See [ADR-002](adr/002-cqrs-meditr.md) for the full decision record.

## DDD Aggregates and Their Boundaries

Domain-Driven Design models business concepts as aggregates — clusters of objects that are always consistent together, modified through a single root, and persisted in a single transaction. This project has three aggregates. `Product` owns its pricing and activation state; the only way to change a price is through `Product.UpdatePrice(Money newPrice)`, which enforces the `Money` value object's invariants (non-negative amount, known currency). `Cart` owns its item collection; `Cart.AddItem` snapshots the current product price at add-time — if the product price changes later, the cart still shows the original price, which is correct e-commerce behavior. `Order` owns its lifecycle; `Order.Cancel()` throws a `DomainException` if the order is not in `Placed` status, making invalid transitions unrepresentable. Aggregate boundaries align with transaction boundaries: no command modifies more than one aggregate root in a single `CommitAsync()`. EF Core has no annotations on domain entities — all mapping lives in `Infrastructure/Persistence/Configurations/`. See [ADR-003](adr/003-ddd-aggregates.md) for the full decision record.

## Conscious Trade-offs

Two architectural decisions deserve explicit justification. First, this is a monolith, not microservices. The e-commerce domain (catalog, cart, orders) maps naturally onto bounded contexts that could be extracted into services — but microservices require distributed transactions, inter-service authentication, distributed tracing, and independent deployment pipelines. For a single-developer portfolio project, those costs are not justified. The monolith deploys with `docker compose up` in one command. Cart checkout and order placement are a single database transaction. See [ADR-004](adr/004-no-microservices.md). Second, this uses Clean Architecture rather than Vertical Slice Architecture (VSA). VSA is a legitimate and increasingly popular alternative that groups all feature code together; it has better ergonomics for large teams. Clean Architecture was chosen here because the primary portfolio signal is demonstrable domain/infrastructure separation — a signal that VSA deliberately de-emphasizes. See [ADR-005](adr/005-clean-arch-vs-vsa.md).

---

## Layer Diagram

```
┌─────────────────────────────────────────────────────────┐
│                       API Layer                         │
│   Minimal APIs · JWT auth · Rate limiting · Scalar UI   │
│   Maps HTTP verbs to MediatR dispatches. No logic.      │
├─────────────────────────────────────────────────────────┤
│                  Application Layer                      │
│   Commands · Queries · Handlers · Validators            │
│   Pipeline: ExceptionHandling → Logging → Validation    │
│   Depends on Domain only.                               │
├─────────────────────────────────────────────────────────┤
│                    Domain Layer                         │
│   Aggregates: Product · Cart · Order                    │
│   Value Objects: Money · CartItem · UserId              │
│   Interfaces: IProductRepository · ICartRepository ·   │
│               IOrderRepository · IUnitOfWork            │
│   Zero NuGet dependencies.                              │
├─────────────────────────────────────────────────────────┤
│                Infrastructure Layer                     │
│   EF Core 10 · PostgreSQL 16 · ASP.NET Identity         │
│   JWT generation · Migrations · Seeding                 │
│   Implements all Domain and Application interfaces.     │
└─────────────────────────────────────────────────────────┘

           Dependencies flow inward only →
```

---

## Layer Boundaries

**Domain** (`src/ECommerce.Domain/`) contains aggregates, value objects, domain exceptions, and the repository and unit-of-work interfaces. It has zero NuGet package dependencies — pure C# types only. The boundary exists to make domain logic independently testable and to prevent business rules from taking an accidental dependency on EF Core, ASP.NET, or any other framework. Everything else in the system depends on Domain; Domain depends on nothing.

**Application** (`src/ECommerce.Application/`) contains all use cases as MediatR `IRequest<TResponse>` records and their handlers, FluentValidation validators, pipeline behaviors, and service interfaces (`IJwtTokenGenerator`, `IUserService`). It depends on Domain only. The boundary exists to separate orchestration (fetch aggregate, call domain method, commit, return DTO) from both the domain rules it orchestrates and the infrastructure it uses. No EF Core, no HTTP primitives, no ASP.NET types.

**Infrastructure** (`src/ECommerce.Infrastructure/`) implements every interface defined in Domain and Application. It contains `AppDbContext`, EF entity configurations, `ProductRepository`, `CartRepository`, `OrderRepository`, `UnitOfWork`, `AuditInterceptor`, `UserService`, and `JwtTokenGenerator`. It depends on Domain and Application. The boundary exists so that swapping PostgreSQL for another database, or replacing JWT with a different auth scheme, is bounded to this layer. No application or domain code changes.

**API** (`src/ECommerce.API/`) is the composition root. Minimal API endpoint groups map HTTP verbs to MediatR dispatches and map results to HTTP responses. `Program.cs` wires all layers together via `AddApplication()` and `AddInfrastructure()`. The boundary exists to keep HTTP concerns out of the application layer: if a handler grows beyond dispatch + response mapping, it has leaked logic that belongs in Application. This layer contains no business logic.
````

- [ ] **Step 2: Verify success criteria**

Confirm:
- 5 paragraphs present (What CA Solves, Why for E-Commerce, Where CQRS Fits, DDD Aggregates, Conscious Trade-offs)
- ASCII diagram present with all 4 layers annotated
- 4 per-layer boundary paragraphs present (Domain, Application, Infrastructure, API)
- ADR cross-references present (ADR-001 through ADR-005)
- Readable in ~3 minutes (roughly 800–1000 words in narrative sections)

- [ ] **Step 3: Commit**

```bash
git add docs/architecture.md
git commit -m "docs: add architecture.md — portfolio narrative, ASCII diagram, layer boundaries"
```

---

## Final Verification

- [ ] **Run full success criteria check**

```bash
# Verify all 7 files exist
ls README.md docs/architecture.md docs/adr/001-clean-architecture.md \
   docs/adr/002-cqrs-meditr.md docs/adr/003-ddd-aggregates.md \
   docs/adr/004-no-microservices.md docs/adr/005-clean-arch-vs-vsa.md
```

Expected: all 7 files listed with no "No such file" errors.

- [ ] **Verify README quick start is in first screenful**

```bash
head -20 README.md
```

Expected: `docker compose up` command visible within first 20 lines.

- [ ] **Verify each ADR has all 4 Nygard sections**

```bash
grep -l "## Status" docs/adr/*.md | wc -l   # expect 5
grep -l "## Context" docs/adr/*.md | wc -l  # expect 5
grep -l "## Decision" docs/adr/*.md | wc -l # expect 5
grep -l "## Consequences" docs/adr/*.md | wc -l # expect 5
```

- [ ] **Verify architecture.md has 5 paragraphs and ASCII diagram**

```bash
grep -c "^## " docs/architecture.md    # expect 7 (intro sections + Layer Diagram + Layer Boundaries)
grep -c "│" docs/architecture.md       # expect > 0 (ASCII box lines)
```

- [ ] **Verify git log shows all commits**

```bash
git log --oneline -10
```

Expected: 7 documentation commits visible (ADRs 001–005, architecture.md, README update).
