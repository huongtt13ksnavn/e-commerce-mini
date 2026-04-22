# Architecture
_Last updated: 2026-04-22_

## Summary

This is a .NET 8 Web API built on Clean Architecture with Domain-Driven Design (DDD) and CQRS via MediatR. The system is split into four projects — Domain, Application, Infrastructure, API — each with a strict unidirectional dependency rule. The Domain layer has zero external dependencies; all runtime wiring flows inward.

## Pattern Overview

**Overall:** Clean Architecture (Onion/Ports & Adapters) with CQRS

**Key Characteristics:**
- Domain layer has no NuGet dependencies; pure C# types only
- Application layer owns all use-cases as MediatR commands/queries
- Infrastructure layer provides all I/O implementations (EF Core, ASP.NET Identity, JWT)
- API layer is thin: maps HTTP routes to MediatR dispatches, handles no business logic

## Layer Dependency Graph

```
ECommerce.API
    └── ECommerce.Application
            └── ECommerce.Domain
ECommerce.Infrastructure
    ├── ECommerce.Application  (implements Application interfaces)
    └── ECommerce.Domain       (implements Domain interfaces)
```

Infrastructure references Application only to implement its interface contracts (`IJwtTokenGenerator`, `IUserService`). The API host wires everything together via `DependencyInjection` extension methods.

## Layers

**Domain (`src/ECommerce.Domain/`):**
- Purpose: Core business rules, entities, value objects, domain exceptions, repository/UoW interfaces
- Location: `src/ECommerce.Domain/`
- Contains: Aggregates, value objects, domain exceptions, `IProductRepository`, `IUnitOfWork`, `IDomainEvent`
- Depends on: Nothing (no external NuGet packages)
- Used by: Application, Infrastructure

**Application (`src/ECommerce.Application/`):**
- Purpose: Use-case orchestration — all CQRS commands, queries, validators, pipeline behaviors, DTOs, and service interfaces
- Location: `src/ECommerce.Application/`
- Contains: MediatR `IRequest`/`IRequestHandler` pairs, FluentValidation validators, `IPipelineBehavior` decorators, `IJwtTokenGenerator`, `IUserService`
- Depends on: Domain only
- Used by: API, Infrastructure (to implement its own interfaces)

**Infrastructure (`src/ECommerce.Infrastructure/`):**
- Purpose: All external I/O — PostgreSQL via EF Core, ASP.NET Identity, JWT generation, seeding/migration
- Location: `src/ECommerce.Infrastructure/`
- Contains: `AppDbContext`, EF entity configurations, `ProductRepository`, `UnitOfWork`, `AuditInterceptor`, `UserService`, `JwtTokenGenerator`, `AppUser`
- Depends on: Domain, Application
- Used by: API (registered at startup)

**API (`src/ECommerce.API/`):**
- Purpose: HTTP surface — Minimal API endpoint groups, middleware, OpenAPI/Scalar docs, startup
- Location: `src/ECommerce.API/`
- Contains: Endpoint static classes, `ExceptionMiddleware`, `Program.cs`
- Depends on: Application, Infrastructure
- Used by: Nothing (top of the composition root)

## CQRS Implementation

All use-cases are MediatR `IRequest<TResponse>` records. Commands mutate state; queries return data. No shared base class distinguishes them — convention only.

**Command example:**
```csharp
// src/ECommerce.Application/Products/Commands/CreateProduct/CreateProductCommand.cs
public sealed record CreateProductCommand(
    string Name, string Description, decimal Price, int Stock, string? ImageUrl)
    : IRequest<Guid>;
```

**Query example:**
```csharp
// src/ECommerce.Application/Products/Queries/GetProducts/GetProductsQuery.cs
// Returns IReadOnlyList<ProductDto> — never returns domain entities
```

Handlers are co-located with their command/query in a single folder (see Structure section).

## MediatR Pipeline Behaviors

Registered in order in `src/ECommerce.Application/DependencyInjection.cs`:

1. `ExceptionHandlingBehavior<TRequest, TResponse>` — logs and re-throws `ValidationException`, `DomainException`, and all unhandled exceptions (`src/ECommerce.Application/Behaviors/ExceptionHandlingBehavior.cs`)
2. `LoggingBehavior<TRequest, TResponse>` — logs request name and elapsed milliseconds (`src/ECommerce.Application/Behaviors/LoggingBehavior.cs`)
3. `ValidationBehavior<TRequest, TResponse>` — runs all FluentValidation validators for the request type; throws `ValidationException` if any fail (`src/ECommerce.Application/Behaviors/ValidationBehavior.cs`)

## Domain Model

**`Product` aggregate (`src/ECommerce.Domain/Entities/Product.cs`):**
- Extends `AggregateRoot` (audit fields + domain event collection)
- Factory method `Product.Create(...)` — only way to construct a valid instance
- `Update(...)` guards against modifying inactive products
- `Deactivate()` performs soft-delete (sets `IsActive = false`)
- `Price` is a `Money` value object — never a raw `decimal`

**`AggregateRoot` (`src/ECommerce.Domain/Common/AggregateRoot.cs`):**
- Implements `IAuditableEntity` (`CreatedAt`, `UpdatedAt`)
- Holds `List<IDomainEvent>` with `RaiseDomainEvent()`/`ClearDomainEvents()`
- `IDomainEvent` extends MediatR `INotification` so events can be published via `IPublisher`

**Value Objects:**
- `Money` (`src/ECommerce.Domain/ValueObjects/Money.cs`) — immutable `record`, guards negative amounts, normalises currency to uppercase; supports `Add()` between same-currency instances
- `UserId` (`src/ECommerce.Domain/ValueObjects/UserId.cs`) — wraps `Guid`; `New()` / `From(string)` factory methods (currently unused in entity layer — identity users rely on ASP.NET Identity's `string` ID)

## Domain Events

`AppDbContext.SaveChangesAsync` dispatches domain events after the DB write completes (`src/ECommerce.Infrastructure/Persistence/AppDbContext.cs`). Events are published via MediatR `IPublisher`. No domain events are raised in the current codebase — the infrastructure is in place for future use.

## Persistence

**ORM:** Entity Framework Core 9 + Npgsql provider

**DbContext:** `AppDbContext` extends `IdentityDbContext<AppUser>` (`src/ECommerce.Infrastructure/Persistence/AppDbContext.cs`). Configurations are loaded with `ApplyConfigurationsFromAssembly`.

**Entity configuration (`src/ECommerce.Infrastructure/Persistence/Configurations/ProductConfiguration.cs`):**
- `Money` mapped as `OwnsOne` → two columns `Price` (decimal 18,2) and `Currency` (varchar 3)
- `Name` has a unique index
- PostgreSQL `xmin` system column used as optimistic concurrency token

**Repository pattern:** `IProductRepository` defined in Domain; `ProductRepository` implemented in Infrastructure. All queries filter `IsActive = true` (soft-delete convention).

**Unit of Work:** `IUnitOfWork.CommitAsync()` delegates to `AppDbContext.SaveChangesAsync`. Commands call `unitOfWork.CommitAsync()` rather than `dbContext.SaveChangesAsync()` directly.

**AuditInterceptor (`src/ECommerce.Infrastructure/Persistence/Interceptors/AuditInterceptor.cs`):** EF `SaveChangesInterceptor` that stamps `CreatedAt` / `UpdatedAt` on all `IAuditableEntity` entries automatically.

**Migrations:** Three incremental migrations in `src/ECommerce.Infrastructure/Persistence/Migrations/`. Applied at startup via `MigrateAndSeedAsync` with a Polly exponential-retry pipeline (5 attempts, 2 s base delay).

## Authentication & Authorization

**Identity:** ASP.NET Core Identity with `AppUser : IdentityUser` stored in PostgreSQL via `AppDbContext`.

**JWT:** Symmetric HMAC-SHA256 tokens (1-hour expiry). `IJwtTokenGenerator` interface in Application; `JwtTokenGenerator` implementation in Infrastructure reads `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience` from configuration.

**Roles:** Two roles — `Admin` and `User`. Seeded at startup. `AdminOnly` policy (`RequireRole("Admin")`) guards all product write endpoints.

**Flow:** Login → `IUserService.ValidateCredentialsAsync` → `IJwtTokenGenerator.Generate` → `LoginResponse(Token, ExpiresAt)`.

## Error Handling Strategy

**Two-layer catch:**

1. `ExceptionHandlingBehavior` (MediatR pipeline) — logs before re-throwing
2. `ExceptionMiddleware` (ASP.NET middleware) — converts exceptions to RFC 7807 `ProblemDetails` JSON:

| Exception | HTTP Status |
|-----------|-------------|
| `ValidationException` | 422 Unprocessable Entity |
| `NotFoundException` | 404 Not Found |
| `DomainException` | 400 Bad Request |
| `DbUpdateConcurrencyException` | 409 Conflict |
| `DbUpdateException` | 422 Unprocessable Entity |
| `UnauthorizedAccessException` | 401 Unauthorized |
| Unhandled `Exception` | 500 Internal Server Error |

## API Surface

Minimal API with endpoint groups. All endpoints are registered in `Program.cs` via extension methods.

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/auth/register` | Anonymous + rate-limit | Register new user |
| POST | `/api/auth/login` | Anonymous + rate-limit | Login, returns JWT |
| GET | `/api/auth/me` | Authenticated | Current user info |
| GET | `/api/products` | Anonymous | List active products |
| GET | `/api/products/{id}` | Anonymous | Get product by ID |
| POST | `/api/products` | AdminOnly | Create product |
| PUT | `/api/products/{id}` | AdminOnly | Update product |
| DELETE | `/api/products/{id}` | AdminOnly | Soft-delete product |
| GET | `/healthz` | Anonymous | Health check (Postgres) |

**Rate limiting:** Fixed-window policy `login` — 5 requests/minute per IP, applied to `/auth/register` and `/auth/login`. Returns HTTP 429 on violation.

## Data Flow — Create Product (Command Path)

1. `POST /api/products` → `ProductEndpoints.CreateAsync` deserialises `CreateProductRequest`
2. Dispatches `CreateProductCommand` via `IMediator.Send`
3. MediatR pipeline: `ExceptionHandlingBehavior` → `LoggingBehavior` → `ValidationBehavior` → `CreateProductCommandHandler`
4. `ValidationBehavior` runs `CreateProductCommandValidator` (FluentValidation) — throws `ValidationException` on failure → caught by `ExceptionMiddleware` → HTTP 422
5. `CreateProductCommandHandler` calls `Product.Create(...)` with `Money.Of(price)`
6. `IProductRepository.Add(product)` stages entity with EF Core
7. `IUnitOfWork.CommitAsync()` → `AppDbContext.SaveChangesAsync` → `AuditInterceptor` stamps timestamps → SQL INSERT
8. Domain events dispatched (none currently raised)
9. Handler returns `product.Id` → endpoint returns HTTP 201 with `Location` header

## Data Flow — Query Path

1. `GET /api/products` → `ProductEndpoints.GetAllAsync`
2. Dispatches `GetProductsQuery` via `IMediator.Send`
3. Pipeline: `ExceptionHandlingBehavior` → `LoggingBehavior` → `GetProductsQueryHandler` (no validator registered)
4. `IProductRepository.GetAllAsync` → EF Core query `WHERE IsActive = true`
5. Handler projects `Product` → `ProductDto` (flattens `Money` to `Amount`/`Currency`)
6. Returns `IReadOnlyList<ProductDto>` → endpoint returns HTTP 200

## Startup & Composition Root

`Program.cs` is the composition root (`src/ECommerce.API/Program.cs`):
1. `AddApplication()` — registers MediatR, pipeline behaviors, FluentValidation validators
2. `AddInfrastructure(config)` — registers EF Core, Identity, repository, UoW, auth services
3. JWT `AddAuthentication` + `AddAuthorization` configured directly in Program.cs
4. Rate limiter registered
5. Health checks (`AddNpgsql`)
6. OpenAPI / Scalar (development only)
7. `MigrateAndSeedAsync` runs before `app.Run()`

---

_Architecture analysis: 2026-04-22_
