# Technology Stack
_Last updated: 2026-04-22_

## Summary
This is a backend-only .NET 10 Web API implementing a Clean Architecture / DDD pattern with four projects: Domain, Application, Infrastructure, and API. There is no frontend — the API is consumed by external clients. The test suite uses xUnit with Testcontainers for integration testing against a real PostgreSQL instance.

## Languages

**Primary:**
- C# 13 (net10.0) — all four source projects and the test project use `<TargetFramework>net10.0</TargetFramework>`

**Secondary:**
- None detected

## Runtime

**Environment:**
- .NET 10.0 (ASP.NET Core)

**Package Manager:**
- NuGet (implicit via `dotnet restore`)
- No lockfile detected (`packages.lock.json` not present)

## Frameworks

**Core:**
- ASP.NET Core 10 (`Microsoft.NET.Sdk.Web`) — HTTP pipeline, minimal API endpoints, middleware
- ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.*) — user/role management, password hashing
- Entity Framework Core 10 (`Microsoft.EntityFrameworkCore` 10.*) — ORM, migrations, interceptors

**CQRS / Mediator:**
- MediatR 12.* — command/query dispatch and domain event publishing

**Validation:**
- FluentValidation 11.* — request validation via MediatR pipeline behaviors
- FluentValidation.DependencyInjectionExtensions 11.* — assembly scanning registration

**Testing:**
- xUnit 2.9.3 — test runner
- Microsoft.AspNetCore.Mvc.Testing 10.* — `WebApplicationFactory` for in-process integration tests
- Testcontainers.PostgreSql 4.11.0 — spins up a real PostgreSQL 16 container per test run
- FluentAssertions 8.9.0 — fluent assertion DSL
- coverlet.collector 6.0.4 — code coverage collection

**Build / Dev:**
- Docker (multi-stage `Dockerfile`, `mcr.microsoft.com/dotnet/sdk:10.0` build image, `mcr.microsoft.com/dotnet/aspnet:10.0` runtime image)
- Docker Compose (`docker-compose.yml`) — local dev stack (PostgreSQL 16-alpine + API)

**Resilience:**
- Polly 8.6.6 — retry pipeline on startup DB migration (`ResiliencePipelineBuilder` with exponential back-off, 5 attempts)

**API Documentation:**
- Microsoft.AspNetCore.OpenApi 10.* — OpenAPI spec generation
- Scalar.AspNetCore 2.14.1 — interactive API reference UI (replaces Swagger UI), served at `/scalar` in Development

**Health Checks:**
- AspNetCore.HealthChecks.NpgSql 9.0.0 — liveness probe against PostgreSQL at `/healthz`

**Logging:**
- `Microsoft.Extensions.Logging.Abstractions` 10.0.6 — logging interfaces used in Application layer
- Console JSON formatter (`"FormatterName": "json"`) — structured JSON logs in production

## Key Dependencies (by project)

**`ECommerce.Domain`:**
- MediatR 12.* — `IDomainEvent` marker / `INotification` for domain events

**`ECommerce.Application`:**
- MediatR 12.* — `IRequest`, `IRequestHandler`, `IPipelineBehavior`, `IPublisher`
- FluentValidation 11.* — validators for commands/queries
- FluentValidation.DependencyInjectionExtensions 11.* — `AddValidatorsFromAssembly`
- Microsoft.Extensions.Logging.Abstractions 10.0.6 — `ILogger<T>` in behaviors

**`ECommerce.Infrastructure`:**
- Microsoft.EntityFrameworkCore 10.* — `DbContext`, `DbSet<T>`, Fluent API config
- Npgsql.EntityFrameworkCore.PostgreSQL 10.* — PostgreSQL EF Core provider
- Microsoft.EntityFrameworkCore.Design 10.* — `dotnet ef` migrations tooling (private asset)
- Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.* — `IdentityDbContext<AppUser>`
- Microsoft.AspNetCore.Authentication.JwtBearer 10.* — JWT validation middleware
- AspNetCore.HealthChecks.NpgSql 9.0.0 — `.AddNpgSql()` health check extension
- Polly 8.6.6 — startup migration retry

**`ECommerce.API`:**
- Microsoft.AspNetCore.OpenApi 10.* — `.AddOpenApi()` / `.MapOpenApi()`
- Microsoft.EntityFrameworkCore.Design 10.* — migrations (private asset)
- Scalar.AspNetCore 2.14.1 — `.MapScalarApiReference()`

**`ECommerce.IntegrationTests`:**
- xUnit 2.9.3
- xunit.runner.visualstudio 3.1.4
- Microsoft.NET.Test.Sdk 17.14.1
- Microsoft.AspNetCore.Mvc.Testing 10.*
- Testcontainers.PostgreSql 4.11.0
- FluentAssertions 8.9.0
- coverlet.collector 6.0.4

## Configuration

**Environment:**
- `appsettings.json` — `src/ECommerce.API/appsettings.json` — default values (localhost PostgreSQL, placeholder JWT secret)
- `appsettings.Development.json` — `src/ECommerce.API/appsettings.Development.json` — dev overrides (verbose EF logging)
- Docker Compose injects `ConnectionStrings__DefaultConnection`, `Jwt__Secret`, `Jwt__Issuer`, `Jwt__Audience` via environment variables
- Required config keys: `ConnectionStrings:DefaultConnection`, `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience`

**Build:**
- No `Directory.Build.props` or `global.json` detected — SDK version is implicit from installed toolchain
- Nullable reference types enabled across all projects (`<Nullable>enable</Nullable>`)
- Implicit usings enabled (`<ImplicitUsings>enable</ImplicitUsings>`)

## Platform Requirements

**Development:**
- .NET 10 SDK
- Docker + Docker Compose (for local PostgreSQL and container-based integration tests)
- PostgreSQL 16 (via Docker or local install)

**Production:**
- Docker container (`mcr.microsoft.com/dotnet/aspnet:10.0` base image)
- Listens on port 8080 inside the container (mapped to host 5000 via Compose)
- CI: GitHub Actions (`ubuntu-latest`), `.github/workflows/ci.yml` — restore → build → test with Docker socket for Testcontainers

---

_Stack analysis: 2026-04-22_
