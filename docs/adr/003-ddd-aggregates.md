# ADR-003: DDD Aggregates

## Status

Accepted

## Context

E-commerce domains have natural invariants that CRUD models cannot express. A cart item must have a positive quantity. A product price must be a valid monetary amount in a known currency. An order cannot be cancelled once it has completed. These rules exist regardless of which HTTP endpoint is called or which developer writes the handler.

The alternative considered was an anemic domain model: plain C# POCOs with public setters, validation living in the Application layer or in FluentValidation validators. This is common and works — but rules enforced in validators can be bypassed by calling the setter directly. The domain model cannot make invalid state unrepresentable.

A second concern: EF Core annotations on domain entities (`[Key]`, `[Required]`, `[Column]`) create a hidden dependency between the domain model and the ORM. The Domain project should have zero knowledge of how it is persisted.

## Decision

Model `Product`, `Cart`, and `Order` as DDD aggregates:

- **Factory methods only** — no public constructors. `Product.Create(name, price)`, `Cart.Create(userId)` are the only way to construct valid instances.
- **Domain methods enforce invariants** — `Cart.AddItem(productId, quantity, unitPrice)` rejects quantity ≤ 0. `Product.Deactivate()` prevents reactivation once deactivated. The rule lives where the data lives.
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
