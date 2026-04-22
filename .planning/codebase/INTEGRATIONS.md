# External Integrations
_Last updated: 2026-04-22_

## Summary
The application integrates with a single external data store (PostgreSQL) and relies on self-issued JWT tokens for authentication — there are no third-party identity providers or external APIs. All integrations are handled inside `ECommerce.Infrastructure` and wired up in `src/ECommerce.Infrastructure/DependencyInjection.cs`.

## APIs & External Services

None detected. The API exposes its own REST endpoints but does not call any external HTTP services or third-party REST/GraphQL APIs.

## Data Storage

**Primary Database:**
- PostgreSQL 16
  - Provider: Npgsql EF Core (`Npgsql.EntityFrameworkCore.PostgreSQL` 10.*)
  - ORM: Entity Framework Core 10 with Fluent API configuration
  - DbContext: `src/ECommerce.Infrastructure/Persistence/AppDbContext.cs`
  - Migrations: `src/ECommerce.Infrastructure/Persistence/Migrations/` (3 migrations: `InitialCreate`, `AddProduct`, `AddProductConstraints`)
  - Entity configuration: `src/ECommerce.Infrastructure/Persistence/Configurations/ProductConfiguration.cs`
  - Connection string env var: `ConnectionStrings__DefaultConnection` (or `ConnectionStrings:DefaultConnection` in JSON)
  - Default dev value: `Host=localhost;Port=5432;Database=ecommerce;Username=postgres;Password=postgres`
  - Docker Compose service: `db` (image `postgres:16-alpine`, volume `postgres_data`)
  - Health check: `/healthz` endpoint backed by `AspNetCore.HealthChecks.NpgSql` 9.0.0

**File Storage:**
- Not applicable — no file/blob storage integration detected.

**Caching:**
- None detected — no Redis, in-memory cache, or distributed cache integration.

## Authentication & Identity

**Provider:** Self-hosted (no external OAuth/OIDC provider)

**Identity Framework:**
- ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.*)
- User entity: `src/ECommerce.Infrastructure/Identity/AppUser.cs` (extends `IdentityUser`)
- Roles: `Admin`, `User` — seeded on startup via `DependencyInjection.SeedRolesAsync`
- Default admin seed: `admin@example.com` / `Admin123!` (development only)
- Password policy: minimum 8 characters, requires non-alphanumeric character, unique email enforced

**Token Issuance:**
- Self-signed JWT (HS256 / HMAC-SHA256)
- Implementation: `src/ECommerce.Infrastructure/Auth/JwtTokenGenerator.cs`
- Interface: `src/ECommerce.Application/Auth/IJwtTokenGenerator.cs`
- Token lifetime: 1 hour
- Claims: `sub` (userId), `email`, `role`, `jti`
- Required config env vars:
  - `Jwt__Secret` (minimum 32 characters, symmetric key)
  - `Jwt__Issuer` (e.g., `ECommerce.API`)
  - `Jwt__Audience` (e.g., `ECommerce.Client`)

**Token Validation:**
- Middleware: `Microsoft.AspNetCore.Authentication.JwtBearer` 10.*
- Configured in `src/ECommerce.API/Program.cs` — validates issuer, audience, lifetime, and signing key

**Authorization:**
- Policy `AdminOnly` — requires `Admin` role claim; applied to product mutation endpoints (Create, Update, Delete)

## Monitoring & Observability

**Health Checks:**
- Endpoint: `GET /healthz`
- Implementation: `src/ECommerce.API/Endpoints/HealthEndpoints.cs`
- Checks: PostgreSQL connectivity via `AspNetCore.HealthChecks.NpgSql`
- Returns HTTP 200 for Healthy/Degraded, 503 for Unhealthy

**Error Tracking:**
- None — no Sentry, Datadog, Application Insights, or equivalent SDK detected.

**Logs:**
- Structured JSON via ASP.NET Core console logger (`"FormatterName": "json"` in `appsettings.json`)
- Log levels: `Default=Information`, `Microsoft.AspNetCore=Warning`, `Microsoft.EntityFrameworkCore=Warning` (production); EF command logging enabled at `Information` in Development
- MediatR pipeline logging behavior: `src/ECommerce.Application/Behaviors/LoggingBehavior.cs`
- No external log aggregation sink detected (no Seq, Elasticsearch, or CloudWatch configuration)

## CI/CD & Deployment

**CI Pipeline:**
- GitHub Actions — `.github/workflows/ci.yml`
- Trigger: push/PR to `master` or `main`
- Runner: `ubuntu-latest`
- Steps: checkout → setup .NET 10 → `dotnet restore` → `dotnet build -c Release` → `dotnet test -c Release`
- Integration tests use `DOCKER_HOST: unix:///var/run/docker.sock` so Testcontainers can spin up PostgreSQL inside the CI runner
- Test results uploaded as artifact (`TestResults/*.trx`)

**Hosting:**
- Docker container via multi-stage `Dockerfile` at `src/ECommerce.API/Dockerfile`
- Base image: `mcr.microsoft.com/dotnet/aspnet:10.0`
- Published port: 8080 (container) → 5000 (host, per Docker Compose)
- Local dev: `docker-compose.yml` at repo root orchestrates `db` (PostgreSQL) and `api` services

**No cloud provider integration detected** (no AWS SDK, Azure SDK, GCP client libraries, or provider-specific deployment config).

## Webhooks & Callbacks

**Incoming:** None detected.
**Outgoing:** None detected.

## Environment Configuration

**Required environment variables / config keys:**

| Key | Description |
|-----|-------------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `Jwt:Secret` | Symmetric signing key (≥ 32 chars) |
| `Jwt:Issuer` | JWT issuer claim value |
| `Jwt:Audience` | JWT audience claim value |

**Secrets location:**
- Development: `appsettings.Development.json` (not committed to production use — placeholder values only)
- Docker Compose: inline `environment:` block in `docker-compose.yml` (development compose file)
- Production: should be injected via environment variables or a secrets manager (no vault/secrets manager integration currently detected)

---

_Integration audit: 2026-04-22_
