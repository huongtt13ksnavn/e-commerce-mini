# .NET 10 E-Commerce Showcase

A production-style e-commerce API built in 5 days to demonstrate Clean Architecture, CQRS, DDD, and AI-assisted development in .NET 10.

[![CI](https://github.com/huongtt13ksnavn/e-commerce-mini/actions/workflows/ci.yml/badge.svg)](https://github.com/huongtt13ksnavn/e-commerce-mini/actions/workflows/ci.yml)

## Quick Start

**Prerequisites:** Docker Desktop

```bash
git clone https://github.com/huongtt13ksnavn/e-commerce-mini.git
cd e-commerce-mini
docker compose up
```

The API starts at **http://localhost:5000**. Explore via the Scalar UI at **http://localhost:5000/scalar**.

**Seed credentials (ready on first boot):**
- Admin: `admin@example.com` / `Admin123!`

## API Overview

| Domain | Endpoints |
|--------|-----------|
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` |
| Products | `GET /api/products`, `GET /api/products/{id}`, `POST/PUT/PATCH` (admin) |
| Cart | `GET/POST/DELETE /api/cart` and `/api/cart/items` (user JWT) |
| Orders | `POST /api/orders`, `GET /api/orders`, `GET /api/orders/{id}`, `PATCH /api/orders/{id}/cancel` |
| Health | `GET /healthz` |

## Why These Patterns

**Clean Architecture** enforces a strict dependency rule: Domain ← Application ← Infrastructure ← API, never reversed (arrows point toward what is depended on). The Domain layer compiles with zero NuGet dependencies — business logic has no knowledge of databases, HTTP, or ASP.NET. This means the domain is testable without mocking EF Core or ASP.NET Identity, and replacing PostgreSQL with another database touches only the Infrastructure layer.

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

## Local Development (without Docker)

**Prerequisites:** .NET 10 SDK, PostgreSQL 16

```bash
# Install EF Core tools (one-time)
dotnet tool install --global dotnet-ef

# Run with local PostgreSQL
dotnet run --project src/ECommerce.API
```

The `appsettings.Development.json` has working dev secrets. Update `ConnectionStrings:DefaultConnection` to point at your local PostgreSQL instance.

## Running Tests

```bash
dotnet test
```

Integration tests use Testcontainers — Docker must be running. First run pulls the PostgreSQL image (~60s).

## Tech Stack

| Concern | Choice |
|---------|--------|
| Runtime | .NET 10, C# 14 |
| API | Minimal APIs |
| CQRS | MediatR 12 |
| Validation | FluentValidation |
| ORM | EF Core 10 |
| Database | PostgreSQL 16 |
| Auth | JWT Bearer |
| Testing | xUnit + Testcontainers |
| API Docs | Scalar |
| CI | GitHub Actions |

## Project Structure

```
src/
  ECommerce.Domain/          No dependencies. Aggregates, value objects, exceptions.
  ECommerce.Application/     References Domain. Commands, queries, MediatR behaviors.
  ECommerce.Infrastructure/  References Domain + Application. EF Core, Identity, JWT.
  ECommerce.API/             References Application + Infrastructure. Endpoints, middleware.
tests/
  ECommerce.IntegrationTests/ xUnit + WebApplicationFactory + Testcontainers.
docs/
  design.md                  Architecture spec and API contract.
  ceo-plan.md                Scope decisions and review history.
  test-plan.md               Integration test coverage plan.
  adr/                       Architecture Decision Records (Day 5).
```
