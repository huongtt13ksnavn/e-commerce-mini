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
