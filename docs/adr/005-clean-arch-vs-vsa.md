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
