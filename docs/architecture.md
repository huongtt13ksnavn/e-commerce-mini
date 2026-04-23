# Architecture

> Portfolio narrative for the .NET 10 E-Commerce Showcase.
> Read time: ~3 minutes.

---

## What Clean Architecture Solves

Clean Architecture is an application of the Dependency Inversion Principle at the structural level. Rather than organizing code by technical role (Models, Services, Controllers), it organizes code by stability: the most stable and most business-critical code — the domain — sits at the center, with no dependencies on anything outside it. Frameworks, databases, and HTTP are details that live at the edges and depend inward. The practical result is a dependency rule: `Domain ← Application ← Infrastructure ← API`, never reversed. In this project that rule is enforced by `.csproj` references. `ECommerce.Domain` has zero NuGet packages. You cannot write an EF Core query in the domain layer because the package is not referenced — the compiler prevents it. This is the key property: the architecture is not a convention that developers must remember to follow; it is a constraint that the build enforces.

## Why for E-Commerce Specifically

E-commerce domains have natural invariants that pure CRUD models cannot protect. A cart item must have a positive quantity. A product price must be a valid monetary amount. A cart item's price is snapshotted at add-time and cannot be silently changed. These rules exist regardless of which endpoint is called or which developer writes the handler. Clean Architecture places these rules in the Domain layer, where they are enforced by domain methods (`Cart.AddItem`, `Product.Deactivate`) rather than validators that can be bypassed. The domain boundary is also the unit of testability: because `ECommerce.Domain` has no infrastructure dependencies, domain logic can be tested with plain `xunit` — no `WebApplicationFactory`, no Testcontainers, no database. The e-commerce domain has two natural aggregate boundaries (Product, Cart), each with its own consistency requirements and lifecycle, which maps cleanly onto Clean Architecture's aggregate-per-repository pattern. See [ADR-001](adr/001-clean-architecture.md) for the full decision record.

## Where CQRS Fits

E-commerce reads vastly outnumber writes. Product listing, cart retrieval, and order history happen on every page load; placing an order or adding a cart item happens occasionally. Command Query Responsibility Segregation (CQRS) makes this asymmetry explicit: commands mutate state via aggregate methods and commit through a Unit of Work; queries return DTOs projected directly from EF Core without touching domain objects. In this codebase, MediatR 12 is the dispatcher — each command and query is a `IRequest<TResponse>` record, and MediatR routes it to the correct handler. The real value of MediatR here is its pipeline: three behaviors run in order for every operation — `ExceptionHandlingBehavior` catches all unhandled exceptions and maps them to ProblemDetails; `LoggingBehavior` logs request name and elapsed time; `ValidationBehavior` runs FluentValidation validators. These cross-cutting concerns apply uniformly to every command without duplication. See [ADR-002](adr/002-cqrs-meditr.md) for the full decision record.

## DDD Aggregate Boundaries

Domain-Driven Design models business concepts as aggregates — clusters of objects that are always consistent together, modified through a single root, and persisted in a single transaction. This project implements two aggregates. `Product` owns its pricing and activation state; the only way to mutate it is through `Product.Update(...)`, which accepts a `Money` value object that enforces its invariants (non-negative amount, known currency). `Cart` owns its item collection; `Cart.AddItem` snapshots the current product price at add-time — if the product price changes later, the cart still shows the original price, which is correct e-commerce behavior. Aggregate boundaries align with transaction boundaries: no command modifies more than one aggregate root in a single `CommitAsync()`. EF Core has no annotations on domain entities — all mapping lives in `Infrastructure/Persistence/Configurations/`. See [ADR-003](adr/003-ddd-aggregates.md) for the full decision record.

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
│   Aggregates: Product · Cart                            │
│   Value Objects: Money · UserId                         │
│   Interfaces: IProductRepository · ICartRepository ·   │
│               IUnitOfWork                               │
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

**Infrastructure** (`src/ECommerce.Infrastructure/`) implements every interface defined in Domain and Application. It contains `AppDbContext`, EF entity configurations, `ProductRepository`, `CartRepository`, `UnitOfWork`, `AuditInterceptor`, `UserService`, and `JwtTokenGenerator`. It depends on Domain and Application. The boundary exists so that swapping PostgreSQL for another database, or replacing JWT with a different auth scheme, is bounded to this layer. No application or domain code changes.

**API** (`src/ECommerce.API/`) is the composition root. Minimal API endpoint groups map HTTP verbs to MediatR dispatches and map results to HTTP responses. `Program.cs` wires all layers together via `AddApplication()` and `AddInfrastructure()`. The boundary exists to keep HTTP concerns out of the application layer: if a handler grows beyond dispatch + response mapping, it has leaked logic that belongs in Application. This layer contains no business logic.
