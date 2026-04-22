# Codebase Structure
_Last updated: 2026-04-22_

## Summary

The solution uses a four-project Clean Architecture layout under `src/`, with one test project under `tests/`. Each project corresponds to one architectural layer. Within Application and Infrastructure, code is grouped by feature (vertical slices) rather than by technical concern.

## Directory Layout

```
e-commerce-mini/
├── src/
│   ├── ECommerce.API/                   # HTTP host, endpoints, middleware
│   │   ├── Endpoints/                   # Minimal API endpoint groups
│   │   │   ├── AuthEndpoints.cs
│   │   │   ├── HealthEndpoints.cs
│   │   │   └── ProductEndpoints.cs
│   │   ├── Middleware/
│   │   │   └── ExceptionMiddleware.cs
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   └── Program.cs                   # Composition root
│   │
│   ├── ECommerce.Application/           # Use-case layer (CQRS, validation, DTOs)
│   │   ├── Auth/                        # Auth feature slice
│   │   │   ├── Commands/
│   │   │   │   └── RegisterUser/
│   │   │   │       ├── RegisterUserCommand.cs
│   │   │   │       ├── RegisterUserCommandHandler.cs
│   │   │   │       └── RegisterUserCommandValidator.cs
│   │   │   ├── Queries/
│   │   │   │   ├── GetCurrentUser/
│   │   │   │   │   ├── GetCurrentUserQuery.cs
│   │   │   │   │   └── GetCurrentUserQueryHandler.cs
│   │   │   │   └── LoginUser/
│   │   │   │       ├── LoginUserQuery.cs
│   │   │   │       ├── LoginUserQueryHandler.cs
│   │   │   │       └── LoginUserQueryValidator.cs
│   │   │   ├── IJwtTokenGenerator.cs    # Port (interface)
│   │   │   └── IUserService.cs          # Port (interface)
│   │   ├── Behaviors/
│   │   │   ├── ExceptionHandlingBehavior.cs
│   │   │   ├── LoggingBehavior.cs
│   │   │   └── ValidationBehavior.cs
│   │   ├── Common/
│   │   │   └── Dtos/
│   │   │       ├── AuthDtos.cs
│   │   │       └── ProductDtos.cs
│   │   ├── Products/                    # Products feature slice
│   │   │   ├── Commands/
│   │   │   │   ├── CreateProduct/
│   │   │   │   │   ├── CreateProductCommand.cs
│   │   │   │   │   ├── CreateProductCommandHandler.cs
│   │   │   │   │   └── CreateProductCommandValidator.cs
│   │   │   │   ├── DeleteProduct/
│   │   │   │   │   ├── DeleteProductCommand.cs
│   │   │   │   │   └── DeleteProductCommandHandler.cs
│   │   │   │   └── UpdateProduct/
│   │   │   │       ├── UpdateProductCommand.cs
│   │   │   │       ├── UpdateProductCommandHandler.cs
│   │   │   │       └── UpdateProductCommandValidator.cs
│   │   │   └── Queries/
│   │   │       ├── GetProduct/
│   │   │       │   ├── GetProductQuery.cs
│   │   │       │   └── GetProductQueryHandler.cs
│   │   │       └── GetProducts/
│   │   │           ├── GetProductsQuery.cs
│   │   │           └── GetProductsQueryHandler.cs
│   │   └── DependencyInjection.cs
│   │
│   ├── ECommerce.Domain/                # Core domain — no external dependencies
│   │   ├── Common/
│   │   │   ├── AggregateRoot.cs
│   │   │   ├── IAuditableEntity.cs
│   │   │   └── IDomainEvent.cs
│   │   ├── Entities/
│   │   │   └── Product.cs
│   │   ├── Exceptions/
│   │   │   ├── CartEmptyException.cs
│   │   │   ├── DomainException.cs
│   │   │   ├── NotFoundException.cs
│   │   │   ├── ProductUnavailableException.cs
│   │   │   └── RegistrationFailedException.cs
│   │   ├── Repositories/
│   │   │   └── IProductRepository.cs
│   │   ├── ValueObjects/
│   │   │   ├── Money.cs
│   │   │   └── UserId.cs
│   │   └── IUnitOfWork.cs
│   │
│   └── ECommerce.Infrastructure/        # I/O implementations
│       ├── Auth/
│       │   ├── JwtTokenGenerator.cs
│       │   └── UserService.cs
│       ├── Identity/
│       │   └── AppUser.cs
│       ├── Persistence/
│       │   ├── Configurations/
│       │   │   └── ProductConfiguration.cs
│       │   ├── Interceptors/
│       │   │   └── AuditInterceptor.cs
│       │   ├── Migrations/
│       │   │   ├── 20260421083656_InitialCreate.cs
│       │   │   ├── 20260422025401_AddProduct.cs
│       │   │   ├── 20260422032106_AddProductConstraints.cs
│       │   │   └── AppDbContextModelSnapshot.cs
│       │   ├── Repositories/
│       │   │   └── ProductRepository.cs
│       │   └── AppDbContext.cs
│       ├── DependencyInjection.cs
│       └── UnitOfWork.cs
│
├── tests/
│   └── ECommerce.IntegrationTests/      # Integration test project (stub)
│       └── UnitTest1.cs
│
├── docs/
├── .planning/
│   └── codebase/                        # GSD codebase maps (this directory)
├── .github/
│   └── workflows/
├── ECommerce.slnx                       # Visual Studio solution
├── docker-compose.yml                   # PostgreSQL + API containers
├── CLAUDE.md
├── TODOS.md
├── VERSION
└── CHANGELOG.md
```

## Project Purposes

**`ECommerce.Domain`:**
- Contains all business rules with zero runtime dependencies
- `Common/` holds base abstractions: `AggregateRoot`, `IAuditableEntity`, `IDomainEvent`
- `Entities/` holds aggregate roots (currently: `Product`)
- `ValueObjects/` holds immutable record types: `Money`, `UserId`
- `Exceptions/` holds the `DomainException` hierarchy
- `Repositories/` holds repository interface contracts (not implementations)
- `IUnitOfWork.cs` lives at the project root (not in a subfolder — single interface, no sub-grouping)

**`ECommerce.Application`:**
- Feature slices under top-level feature folders (`Auth/`, `Products/`)
- Each use-case lives in its own subfolder: `Commands/{UseCaseName}/` or `Queries/{UseCaseName}/`
- Each use-case folder contains up to three files: `*Command.cs` / `*Query.cs`, `*Handler.cs`, `*Validator.cs`
- `Behaviors/` contains cross-cutting MediatR pipeline decorators
- `Common/Dtos/` contains all request/response DTOs shared across the API boundary
- Service interfaces (`IJwtTokenGenerator`, `IUserService`) live directly in `Auth/` (not in a `Interfaces/` folder)

**`ECommerce.Infrastructure`:**
- `Auth/` implements Application service interfaces: `JwtTokenGenerator`, `UserService`
- `Identity/` contains `AppUser` (ASP.NET Identity entity, not a domain entity)
- `Persistence/AppDbContext.cs` is the single EF Core context
- `Persistence/Configurations/` contains one `IEntityTypeConfiguration<T>` class per entity
- `Persistence/Interceptors/` contains EF `SaveChangesInterceptor` implementations
- `Persistence/Migrations/` contains auto-generated EF migration files (do not edit manually)
- `Persistence/Repositories/` contains concrete repository implementations
- `UnitOfWork.cs` lives at the project root alongside `DependencyInjection.cs`

**`ECommerce.API`:**
- `Endpoints/` contains one static class per feature group, each with a `Map*Endpoints` extension method
- `Middleware/` contains custom ASP.NET middleware (only `ExceptionMiddleware`)
- `Program.cs` is the only entry point and composition root — no `Startup.cs`

## Key File Locations

**Entry point:**
- `src/ECommerce.API/Program.cs` — startup, middleware pipeline, DI registration, endpoint mapping

**Domain model:**
- `src/ECommerce.Domain/Entities/Product.cs` — sole aggregate root
- `src/ECommerce.Domain/ValueObjects/Money.cs` — price value object
- `src/ECommerce.Domain/Common/AggregateRoot.cs` — base class for aggregates

**Persistence:**
- `src/ECommerce.Infrastructure/Persistence/AppDbContext.cs` — EF Core context
- `src/ECommerce.Infrastructure/Persistence/Configurations/ProductConfiguration.cs` — EF mapping
- `src/ECommerce.Infrastructure/Persistence/Repositories/ProductRepository.cs` — repository impl

**Auth:**
- `src/ECommerce.Application/Auth/IJwtTokenGenerator.cs` — token interface
- `src/ECommerce.Application/Auth/IUserService.cs` — identity service interface
- `src/ECommerce.Infrastructure/Auth/JwtTokenGenerator.cs` — JWT implementation
- `src/ECommerce.Infrastructure/Auth/UserService.cs` — Identity implementation

**DI registration:**
- `src/ECommerce.Application/DependencyInjection.cs` — Application layer services
- `src/ECommerce.Infrastructure/DependencyInjection.cs` — Infrastructure layer services (also contains `MigrateAndSeedAsync`)

## Naming Conventions

**Projects:**
- Pattern: `ECommerce.{Layer}` (PascalCase, dot-separated)
- Examples: `ECommerce.Domain`, `ECommerce.Application`, `ECommerce.Infrastructure`, `ECommerce.API`

**Namespaces:**
- Mirror project and folder path: `ECommerce.Application.Products.Commands.CreateProduct`
- No namespace aliases used

**Files — Commands/Queries:**
- Pattern: `{UseCase}{Command|Query}.cs` + `{UseCase}{Command|Query}Handler.cs` + `{UseCase}{Command|Query}Validator.cs`
- Examples: `CreateProductCommand.cs`, `CreateProductCommandHandler.cs`, `CreateProductCommandValidator.cs`

**Files — Entities:**
- Singular noun: `Product.cs`

**Files — Interfaces:**
- `I` prefix: `IProductRepository.cs`, `IUnitOfWork.cs`, `IJwtTokenGenerator.cs`

**Files — Configurations:**
- Pattern: `{Entity}Configuration.cs` → `ProductConfiguration.cs`

**Files — DTOs:**
- Pattern: `{Feature}Dtos.cs` — multiple records in one file: `ProductDtos.cs`, `AuthDtos.cs`

**Classes:**
- `sealed` by default unless designed for inheritance
- Factory methods named `Create(...)` on aggregates
- Value objects are `sealed record`

**Endpoint groups:**
- Pattern: `{Feature}Endpoints.cs` with `Map{Feature}Endpoints(this IEndpointRouteBuilder app)` extension method

## Module Grouping Approach

Application and Infrastructure both use **vertical feature slices** as the primary grouping axis:

```
Products/
  Commands/
    CreateProduct/   ← one folder per use-case
    UpdateProduct/
    DeleteProduct/
  Queries/
    GetProduct/
    GetProducts/
```

Cross-feature concerns (behaviors, common DTOs) live in dedicated horizontal folders (`Behaviors/`, `Common/`).

Domain uses **technical concern** grouping because it has no feature duplication (`Entities/`, `ValueObjects/`, `Exceptions/`, `Repositories/`).

## Where to Add New Code

**New aggregate (e.g., `Order`):**
- Domain entity: `src/ECommerce.Domain/Entities/Order.cs` (extends `AggregateRoot`)
- Repository interface: `src/ECommerce.Domain/Repositories/IOrderRepository.cs`
- EF configuration: `src/ECommerce.Infrastructure/Persistence/Configurations/OrderConfiguration.cs`
- Repository impl: `src/ECommerce.Infrastructure/Persistence/Repositories/OrderRepository.cs`
- Register in: `src/ECommerce.Infrastructure/DependencyInjection.cs`
- Add `DbSet<Order>` to: `src/ECommerce.Infrastructure/Persistence/AppDbContext.cs`

**New use-case (e.g., `PlaceOrder` command):**
- Folder: `src/ECommerce.Application/Orders/Commands/PlaceOrder/`
- Files: `PlaceOrderCommand.cs`, `PlaceOrderCommandHandler.cs`, `PlaceOrderCommandValidator.cs`
- DTO: add records to `src/ECommerce.Application/Common/Dtos/OrderDtos.cs`

**New endpoint group:**
- File: `src/ECommerce.API/Endpoints/OrderEndpoints.cs`
- Register: `app.MapOrderEndpoints()` in `src/ECommerce.API/Program.cs`

**New domain exception:**
- File: `src/ECommerce.Domain/Exceptions/{ExceptionName}.cs` (extends `DomainException`)
- Map to HTTP status in: `src/ECommerce.API/Middleware/ExceptionMiddleware.cs`

**New value object:**
- File: `src/ECommerce.Domain/ValueObjects/{Name}.cs` (use `sealed record`)

**New infrastructure service:**
- Interface: `src/ECommerce.Application/{Feature}/I{ServiceName}.cs`
- Implementation: `src/ECommerce.Infrastructure/{Feature}/{ServiceName}.cs`
- Register: `src/ECommerce.Infrastructure/DependencyInjection.cs`

**New EF migration:**
```bash
dotnet ef migrations add {MigrationName} \
  --project src/ECommerce.Infrastructure \
  --startup-project src/ECommerce.API
```

## Special Directories

**`src/ECommerce.Infrastructure/Persistence/Migrations/`:**
- Purpose: Auto-generated EF Core migration files
- Generated: Yes (via `dotnet ef migrations add`)
- Committed: Yes
- Do not edit manually; always regenerate via EF tooling

**`.planning/codebase/`:**
- Purpose: GSD codebase analysis documents
- Generated: Yes (by GSD mapping agents)
- Committed: Yes

**`docs/`:**
- Purpose: Project documentation (currently empty/minimal)
- Generated: No

---

_Structure analysis: 2026-04-22_
