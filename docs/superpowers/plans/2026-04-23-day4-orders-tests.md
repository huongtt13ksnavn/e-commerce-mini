# Day 4 — Orders + Integration Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the Order aggregate, four order endpoints (place/list/get/cancel), and the full 13-test integration suite needed to pass the Day 4 CI gate.

**Architecture:** Order is a DDD aggregate root in the Domain layer, persisted via EF owned collection for OrderItems (mirrors CartItem pattern). PlaceOrderCommand handler reads Cart, creates Order, clears Cart, and commits in one unit of work — triggering the OrderPlaced domain event via AppDbContext. Three new test files (OrderTests, AuthTests, ProductTests) complete the 13-test coverage required by design.md.

**Tech Stack:** .NET 10, C# 14, MediatR 12, EF Core 10 + Npgsql, FluentValidation, xUnit + WebApplicationFactory + Testcontainers, FluentAssertions

---

## Pre-Flight Check

Run this before starting. All must pass.

```bash
cd "E:/Project For Fun/Ecomerce/e-commerce-mini"
dotnet build
dotnet test
```

Expected: build succeeds, existing tests green. If not, fix first.

---

## Task 1: Domain Primitives — OrderStatus, OrderItem, OrderItemData

**Files:**
- Create: `src/ECommerce.Domain/Enums/OrderStatus.cs`
- Create: `src/ECommerce.Domain/Entities/OrderItem.cs`
- Create: `src/ECommerce.Domain/Entities/OrderItemData.cs`

- [ ] **Step 1: Create OrderStatus enum**

```csharp
// src/ECommerce.Domain/Enums/OrderStatus.cs
namespace ECommerce.Domain.Enums;

public enum OrderStatus
{
    Pending,
    Completed,
    Cancelled,
}
```

- [ ] **Step 2: Create OrderItem owned entity**

Mirrors `CartItem` — snapshot of product state at order time, owned by Order.

```csharp
// src/ECommerce.Domain/Entities/OrderItem.cs
using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Entities;

public sealed class OrderItem
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = null!;

    private OrderItem() { }

    internal static OrderItem Create(Guid productId, string productName, int quantity, Money unitPrice) =>
        new() { ProductId = productId, ProductName = productName, Quantity = quantity, UnitPrice = unitPrice };
}
```

- [ ] **Step 3: Create OrderItemData transient record**

Used only in the `PlaceOrder` factory call — carries snapshot data from handler to aggregate. Not persisted.

```csharp
// src/ECommerce.Domain/Entities/OrderItemData.cs
using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Entities;

public record OrderItemData(Guid ProductId, string ProductName, int Quantity, Money UnitPrice);
```

- [ ] **Step 4: Build to verify no errors**

```bash
dotnet build src/ECommerce.Domain
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/ECommerce.Domain/Enums/OrderStatus.cs \
        src/ECommerce.Domain/Entities/OrderItem.cs \
        src/ECommerce.Domain/Entities/OrderItemData.cs
git commit -m "feat: add OrderStatus enum, OrderItem, OrderItemData"
```

---

## Task 2: Order Aggregate

**Files:**
- Create: `src/ECommerce.Domain/Entities/Order.cs`

- [ ] **Step 1: Create Order aggregate**

```csharp
// src/ECommerce.Domain/Entities/Order.cs
using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Events;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Entities;

public sealed class Order : AggregateRoot
{
    public Guid Id { get; private set; }
    public UserId UserId { get; private set; } = null!;
    public OrderStatus Status { get; private set; }
    public Money Total { get; private set; } = null!;
    public DateTime PlacedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    private List<OrderItem> _items = [];
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    private Order() { }

    public static Order PlaceOrder(UserId userId, IEnumerable<OrderItemData> items)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0)
            throw new CartEmptyException();

        var currency = itemList[0].UnitPrice.Currency;
        var totalAmount = itemList.Sum(i => i.UnitPrice.Amount * i.Quantity);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = OrderStatus.Pending,
            Total = Money.Of(totalAmount, currency),
            PlacedAt = DateTime.UtcNow,
            _items = itemList
                .Select(i => OrderItem.Create(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice))
                .ToList(),
        };

        order.RaiseDomainEvent(new OrderPlaced(order.Id));
        return order;
    }

    public void Cancel()
    {
        if (Status == OrderStatus.Completed)
            throw new OrderAlreadyCompletedException();
        if (Status == OrderStatus.Cancelled)
            return;
        Status = OrderStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 2: Skip build for now — Order.cs references OrderPlaced and OrderAlreadyCompletedException created in Task 3. Commit both together at the end of Task 3.**

---

## Task 3: Domain Events, Repository Interface, New Exceptions

**Files:**
- Create: `src/ECommerce.Domain/Events/OrderPlaced.cs`
- Create: `src/ECommerce.Domain/Repositories/IOrderRepository.cs`
- Create: `src/ECommerce.Domain/Exceptions/OrderNotFoundException.cs`
- Create: `src/ECommerce.Domain/Exceptions/OrderAlreadyCompletedException.cs`

Note: `CartNotFoundException` already exists at `src/ECommerce.Domain/Exceptions/CartNotFoundException.cs`. Do NOT recreate it.

- [ ] **Step 1: Create OrderPlaced domain event**

```csharp
// src/ECommerce.Domain/Events/OrderPlaced.cs
using ECommerce.Domain.Common;

namespace ECommerce.Domain.Events;

public record OrderPlaced(Guid OrderId) : IDomainEvent;
```

- [ ] **Step 2: Create IOrderRepository**

```csharp
// src/ECommerce.Domain/Repositories/IOrderRepository.cs
using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Repositories;

public interface IOrderRepository
{
    /// <summary>Returns null if not found OR if the order belongs to a different user.</summary>
    Task<Order?> GetByIdAsync(Guid orderId, UserId userId, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetByUserIdAsync(UserId userId, CancellationToken ct = default);
    void Add(Order order);
}
```

- [ ] **Step 3: Create OrderNotFoundException**

Extends `NotFoundException` so the existing `ExceptionMiddleware` maps it to 404 — no middleware changes required.

```csharp
// src/ECommerce.Domain/Exceptions/OrderNotFoundException.cs
namespace ECommerce.Domain.Exceptions;

public sealed class OrderNotFoundException() : NotFoundException("Order", string.Empty);
```

- [ ] **Step 4: Create OrderAlreadyCompletedException**

```csharp
// src/ECommerce.Domain/Exceptions/OrderAlreadyCompletedException.cs
namespace ECommerce.Domain.Exceptions;

public sealed class OrderAlreadyCompletedException()
    : DomainException("Order is already completed and cannot be cancelled.");
```

- [ ] **Step 5: Build — should be clean now**

```bash
dotnet build src/ECommerce.Domain
```

Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 6: Commit Order aggregate + all Task 3 files together**

```bash
git add src/ECommerce.Domain/Entities/Order.cs \
        src/ECommerce.Domain/Events/OrderPlaced.cs \
        src/ECommerce.Domain/Repositories/IOrderRepository.cs \
        src/ECommerce.Domain/Exceptions/OrderNotFoundException.cs \
        src/ECommerce.Domain/Exceptions/OrderAlreadyCompletedException.cs
git commit -m "feat: add Order aggregate, OrderPlaced event, IOrderRepository, new exceptions"
```

---

## Task 4: Infrastructure — Configuration, Repository, AppDbContext, DI

**Files:**
- Create: `src/ECommerce.Infrastructure/Persistence/Configurations/OrderConfiguration.cs`
- Create: `src/ECommerce.Infrastructure/Persistence/Repositories/OrderRepository.cs`
- Modify: `src/ECommerce.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `src/ECommerce.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Create OrderConfiguration**

Mirrors `CartConfiguration` pattern. OrderItems use a shadow int "Id" key since they have no natural unique key within an Order.

```csharp
// src/ECommerce.Infrastructure/Persistence/Configurations/OrderConfiguration.cs
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(o => o.PlacedAt).IsRequired();
        builder.Property(o => o.CancelledAt);

        builder.OwnsOne(o => o.Total, total =>
        {
            total.Property(m => m.Amount)
                .HasColumnName("TotalAmount")
                .HasPrecision(18, 2)
                .IsRequired();
            total.Property(m => m.Currency)
                .HasColumnName("TotalCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsMany(o => o.Items, item =>
        {
            item.ToTable("OrderItems");
            item.Property<int>("Id").ValueGeneratedOnAdd();
            item.HasKey("Id");
            item.WithOwner().HasForeignKey("OrderId");

            item.Property(i => i.ProductId).IsRequired();
            item.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
            item.Property(i => i.Quantity).IsRequired();

            item.OwnsOne(i => i.UnitPrice, price =>
            {
                price.Property(m => m.Amount)
                    .HasColumnName("UnitPrice")
                    .HasPrecision(18, 2)
                    .IsRequired();
                price.Property(m => m.Currency)
                    .HasColumnName("Currency")
                    .HasMaxLength(3)
                    .IsRequired();
            });
        });

        builder.Navigation(o => o.Items).HasField("_items");
        builder.HasIndex(o => o.UserId).HasDatabaseName("ix_orders_user_id");
    }
}
```

- [ ] **Step 2: Create OrderRepository**

```csharp
// src/ECommerce.Infrastructure/Persistence/Repositories/OrderRepository.cs
using ECommerce.Domain.Entities;
using ECommerce.Domain.Repositories;
using ECommerce.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository(AppDbContext dbContext) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(Guid orderId, UserId userId, CancellationToken ct = default) =>
        dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);

    public async Task<IReadOnlyList<Order>> GetByUserIdAsync(UserId userId, CancellationToken ct = default) =>
        await dbContext.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.PlacedAt)
            .ToListAsync(ct);

    public void Add(Order order) => dbContext.Orders.Add(order);
}
```

- [ ] **Step 3: Add Orders DbSet to AppDbContext**

Open `src/ECommerce.Infrastructure/Persistence/AppDbContext.cs` and add the Orders property after the Carts line:

```csharp
// Before:
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Cart> Carts => Set<Cart>();

// After:
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<Order> Orders => Set<Order>();
```

Add the missing using at the top of the file if needed — `Order` is in `ECommerce.Domain.Entities` which is already imported.

- [ ] **Step 4: Register IOrderRepository in DependencyInjection.cs**

Open `src/ECommerce.Infrastructure/DependencyInjection.cs` and add after the ICartRepository line:

```csharp
// Before:
        services.AddScoped<ICartRepository, CartRepository>();

// After:
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
```

Add the missing using:
```csharp
using ECommerce.Domain.Repositories;
```
(if not already present — check the existing usings at top of file).

- [ ] **Step 5: Build infrastructure to verify**

```bash
dotnet build src/ECommerce.Infrastructure
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/ECommerce.Infrastructure/Persistence/Configurations/OrderConfiguration.cs \
        src/ECommerce.Infrastructure/Persistence/Repositories/OrderRepository.cs \
        src/ECommerce.Infrastructure/Persistence/AppDbContext.cs \
        src/ECommerce.Infrastructure/DependencyInjection.cs
git commit -m "feat: add OrderConfiguration, OrderRepository; wire up AppDbContext and DI"
```

---

## Task 5: EF Core Migration

**Files:**
- Create: EF migration (generated by CLI)

- [ ] **Step 1: Generate migration**

Run from the solution root:

```bash
dotnet ef migrations add AddOrder \
  --project src/ECommerce.Infrastructure \
  --startup-project src/ECommerce.API
```

Expected output contains:
```
Done. To undo this action, use 'ef migrations remove'
```

A new file `src/ECommerce.Infrastructure/Persistence/Migrations/YYYYMMDDHHMMSS_AddOrder.cs` is created.

- [ ] **Step 2: Inspect the generated migration**

Open the generated migration file. Verify it creates:
- `Orders` table with columns: `Id`, `UserId`, `Status`, `TotalAmount`, `TotalCurrency`, `PlacedAt`, `CancelledAt`
- `OrderItems` table with columns: `Id` (int, identity), `OrderId` (FK), `ProductId`, `ProductName`, `Quantity`, `UnitPrice`, `Currency`
- Index `ix_orders_user_id` on `Orders.UserId`

If the migration looks wrong (missing tables, wrong column names), run `dotnet ef migrations remove` and revisit Task 4 config.

- [ ] **Step 3: Build the full solution**

```bash
dotnet build
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/ECommerce.Infrastructure/Persistence/Migrations/
git commit -m "feat: add EF migration AddOrder"
```

---

## Task 6: Application — Order DTOs

**Files:**
- Create: `src/ECommerce.Application/Common/Dtos/OrderDtos.cs`

- [ ] **Step 1: Create OrderDtos**

```csharp
// src/ECommerce.Application/Common/Dtos/OrderDtos.cs
namespace ECommerce.Application.Common.Dtos;

public sealed record OrderItemDto(
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    string Currency);

public sealed record OrderSummaryDto(
    Guid OrderId,
    string Status,
    decimal Total,
    string Currency,
    DateTime PlacedAt,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderDetailDto(
    Guid Id,
    string Status,
    decimal Total,
    string Currency,
    DateTime PlacedAt,
    DateTime? CancelledAt,
    IReadOnlyList<OrderItemDto> Items);
```

- [ ] **Step 2: Commit**

```bash
git add src/ECommerce.Application/Common/Dtos/OrderDtos.cs
git commit -m "feat: add OrderItemDto, OrderSummaryDto, OrderDetailDto"
```

---

## Task 7: Application — PlaceOrderCommand

**Files:**
- Create: `src/ECommerce.Application/Orders/Commands/PlaceOrder/PlaceOrderCommand.cs`
- Create: `src/ECommerce.Application/Orders/Commands/PlaceOrder/PlaceOrderCommandHandler.cs`
- Create: `src/ECommerce.Application/Orders/Commands/PlaceOrder/PlaceOrderCommandValidator.cs`

- [ ] **Step 1: Create PlaceOrderCommand**

```csharp
// src/ECommerce.Application/Orders/Commands/PlaceOrder/PlaceOrderCommand.cs
using ECommerce.Domain.ValueObjects;
using MediatR;

namespace ECommerce.Application.Orders.Commands.PlaceOrder;

public sealed record PlaceOrderCommand(UserId UserId) : IRequest<Guid>;
```

- [ ] **Step 2: Create PlaceOrderCommandHandler**

Handler flow: get cart (null = empty = 400) → verify all products active → build snapshots → create order → clear cart → commit → return orderId.

```csharp
// src/ECommerce.Application/Orders/Commands/PlaceOrder/PlaceOrderCommandHandler.cs
using ECommerce.Domain;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.Repositories;
using MediatR;

namespace ECommerce.Application.Orders.Commands.PlaceOrder;

public sealed class PlaceOrderCommandHandler(
    ICartRepository cartRepository,
    IProductRepository productRepository,
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<PlaceOrderCommand, Guid>
{
    public async Task<Guid> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var cart = await cartRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
            throw new CartEmptyException();

        var orderItems = new List<OrderItemData>();
        foreach (var cartItem in cart.Items)
        {
            var product = await productRepository.GetByIdAsync(cartItem.ProductId, cancellationToken)
                ?? throw new NotFoundException("Product", cartItem.ProductId);
            if (!product.IsActive)
                throw new ProductUnavailableException(product.Id);
            orderItems.Add(new OrderItemData(product.Id, product.Name, cartItem.Quantity, cartItem.UnitPrice));
        }

        var order = Order.PlaceOrder(request.UserId, orderItems);
        orderRepository.Add(order);
        cart.Clear();
        await unitOfWork.CommitAsync(cancellationToken);
        return order.Id;
    }
}
```

- [ ] **Step 3: Create PlaceOrderCommandValidator**

```csharp
// src/ECommerce.Application/Orders/Commands/PlaceOrder/PlaceOrderCommandValidator.cs
using FluentValidation;

namespace ECommerce.Application.Orders.Commands.PlaceOrder;

public sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.UserId.Value).NotEmpty();
    }
}
```

- [ ] **Step 4: Build**

```bash
dotnet build src/ECommerce.Application
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/ECommerce.Application/Orders/Commands/PlaceOrder/
git commit -m "feat: add PlaceOrderCommand, Handler, Validator"
```

---

## Task 8: Application — CancelOrderCommand

**Files:**
- Create: `src/ECommerce.Application/Orders/Commands/CancelOrder/CancelOrderCommand.cs`
- Create: `src/ECommerce.Application/Orders/Commands/CancelOrder/CancelOrderCommandHandler.cs`
- Create: `src/ECommerce.Application/Orders/Commands/CancelOrder/CancelOrderCommandValidator.cs`

- [ ] **Step 1: Create CancelOrderCommand**

```csharp
// src/ECommerce.Application/Orders/Commands/CancelOrder/CancelOrderCommand.cs
using ECommerce.Domain.ValueObjects;
using MediatR;

namespace ECommerce.Application.Orders.Commands.CancelOrder;

public sealed record CancelOrderCommand(Guid OrderId, UserId UserId) : IRequest;
```

- [ ] **Step 2: Create CancelOrderCommandHandler**

```csharp
// src/ECommerce.Application/Orders/Commands/CancelOrder/CancelOrderCommandHandler.cs
using ECommerce.Domain;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.Repositories;
using MediatR;

namespace ECommerce.Application.Orders.Commands.CancelOrder;

public sealed class CancelOrderCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CancelOrderCommand>
{
    public async Task Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, request.UserId, cancellationToken)
            ?? throw new OrderNotFoundException();
        order.Cancel();
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
```

- [ ] **Step 3: Create CancelOrderCommandValidator**

```csharp
// src/ECommerce.Application/Orders/Commands/CancelOrder/CancelOrderCommandValidator.cs
using FluentValidation;

namespace ECommerce.Application.Orders.Commands.CancelOrder;

public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.UserId.Value).NotEmpty();
    }
}
```

- [ ] **Step 4: Build**

```bash
dotnet build src/ECommerce.Application
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/ECommerce.Application/Orders/Commands/CancelOrder/
git commit -m "feat: add CancelOrderCommand, Handler, Validator"
```

---

## Task 9: Application — GetOrders and GetOrder Queries

**Files:**
- Create: `src/ECommerce.Application/Orders/Queries/GetOrders/GetOrdersQuery.cs`
- Create: `src/ECommerce.Application/Orders/Queries/GetOrders/GetOrdersQueryHandler.cs`
- Create: `src/ECommerce.Application/Orders/Queries/GetOrder/GetOrderQuery.cs`
- Create: `src/ECommerce.Application/Orders/Queries/GetOrder/GetOrderQueryHandler.cs`

- [ ] **Step 1: Create GetOrdersQuery**

```csharp
// src/ECommerce.Application/Orders/Queries/GetOrders/GetOrdersQuery.cs
using ECommerce.Application.Common.Dtos;
using ECommerce.Domain.ValueObjects;
using MediatR;

namespace ECommerce.Application.Orders.Queries.GetOrders;

public sealed record GetOrdersQuery(UserId UserId) : IRequest<IReadOnlyList<OrderSummaryDto>>;
```

- [ ] **Step 2: Create GetOrdersQueryHandler**

```csharp
// src/ECommerce.Application/Orders/Queries/GetOrders/GetOrdersQueryHandler.cs
using ECommerce.Application.Common.Dtos;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Repositories;
using MediatR;

namespace ECommerce.Application.Orders.Queries.GetOrders;

public sealed class GetOrdersQueryHandler(IOrderRepository orderRepository)
    : IRequestHandler<GetOrdersQuery, IReadOnlyList<OrderSummaryDto>>
{
    public async Task<IReadOnlyList<OrderSummaryDto>> Handle(
        GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await orderRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        return orders.Select(MapToSummary).ToList().AsReadOnly();
    }

    private static OrderSummaryDto MapToSummary(Order o) => new(
        o.Id,
        o.Status.ToString(),
        o.Total.Amount,
        o.Total.Currency,
        o.PlacedAt,
        o.Items
            .Select(i => new OrderItemDto(i.ProductName, i.Quantity, i.UnitPrice.Amount, i.UnitPrice.Currency))
            .ToList()
            .AsReadOnly());
}
```

- [ ] **Step 3: Create GetOrderQuery**

```csharp
// src/ECommerce.Application/Orders/Queries/GetOrder/GetOrderQuery.cs
using ECommerce.Application.Common.Dtos;
using ECommerce.Domain.ValueObjects;
using MediatR;

namespace ECommerce.Application.Orders.Queries.GetOrder;

public sealed record GetOrderQuery(Guid OrderId, UserId UserId) : IRequest<OrderDetailDto>;
```

- [ ] **Step 4: Create GetOrderQueryHandler**

```csharp
// src/ECommerce.Application/Orders/Queries/GetOrder/GetOrderQueryHandler.cs
using ECommerce.Application.Common.Dtos;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.Repositories;
using MediatR;

namespace ECommerce.Application.Orders.Queries.GetOrder;

public sealed class GetOrderQueryHandler(IOrderRepository orderRepository)
    : IRequestHandler<GetOrderQuery, OrderDetailDto>
{
    public async Task<OrderDetailDto> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, request.UserId, cancellationToken)
            ?? throw new OrderNotFoundException();

        return new OrderDetailDto(
            order.Id,
            order.Status.ToString(),
            order.Total.Amount,
            order.Total.Currency,
            order.PlacedAt,
            order.CancelledAt,
            order.Items
                .Select(i => new OrderItemDto(i.ProductName, i.Quantity, i.UnitPrice.Amount, i.UnitPrice.Currency))
                .ToList()
                .AsReadOnly());
    }
}
```

- [ ] **Step 5: Build**

```bash
dotnet build src/ECommerce.Application
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/ECommerce.Application/Orders/Queries/
git commit -m "feat: add GetOrdersQuery, GetOrderQuery, and handlers"
```

---

## Task 10: Application — OrderPlacedNotificationHandler

**Files:**
- Create: `src/ECommerce.Application/Orders/Events/OrderPlacedNotificationHandler.cs`

- [ ] **Step 1: Create handler**

```csharp
// src/ECommerce.Application/Orders/Events/OrderPlacedNotificationHandler.cs
using ECommerce.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ECommerce.Application.Orders.Events;

public sealed class OrderPlacedNotificationHandler(ILogger<OrderPlacedNotificationHandler> logger)
    : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Order placed: {OrderId}", notification.OrderId);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Build full solution**

```bash
dotnet build
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/ECommerce.Application/Orders/Events/OrderPlacedNotificationHandler.cs
git commit -m "feat: add OrderPlacedNotificationHandler (stub — logs only)"
```

---

## Task 11: API — OrderEndpoints + Program.cs

**Files:**
- Create: `src/ECommerce.API/Endpoints/OrderEndpoints.cs`
- Modify: `src/ECommerce.API/Program.cs`

- [ ] **Step 1: Create OrderEndpoints**

Mirrors `CartEndpoints` exactly — same `TryResolveUserId` helper, same `RequireAuthorization()` pattern.

```csharp
// src/ECommerce.API/Endpoints/OrderEndpoints.cs
using ECommerce.Application.Common.Dtos;
using ECommerce.Application.Orders.Commands.CancelOrder;
using ECommerce.Application.Orders.Commands.PlaceOrder;
using ECommerce.Application.Orders.Queries.GetOrder;
using ECommerce.Application.Orders.Queries.GetOrders;
using ECommerce.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ECommerce.API.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/orders", PlaceOrderAsync)
            .WithTags("Orders")
            .RequireAuthorization()
            .Produces(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/orders", GetOrdersAsync)
            .WithTags("Orders")
            .RequireAuthorization()
            .Produces<IReadOnlyList<OrderSummaryDto>>()
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/orders/{id}", GetOrderAsync)
            .WithTags("Orders")
            .RequireAuthorization()
            .Produces<OrderDetailDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapMethods("/api/orders/{id}/cancel", ["PATCH"], CancelOrderAsync)
            .WithTags("Orders")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> PlaceOrderAsync(
        ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        if (!TryResolveUserId(user, out var userId)) return Results.Unauthorized();
        var orderId = await mediator.Send(new PlaceOrderCommand(userId), ct);
        return Results.Created($"/api/orders/{orderId}", new { orderId });
    }

    private static async Task<IResult> GetOrdersAsync(
        ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        if (!TryResolveUserId(user, out var userId)) return Results.Unauthorized();
        var orders = await mediator.Send(new GetOrdersQuery(userId), ct);
        return Results.Ok(orders);
    }

    private static async Task<IResult> GetOrderAsync(
        Guid id, ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        if (!TryResolveUserId(user, out var userId)) return Results.Unauthorized();
        var order = await mediator.Send(new GetOrderQuery(id, userId), ct);
        return Results.Ok(order);
    }

    private static async Task<IResult> CancelOrderAsync(
        Guid id, ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        if (!TryResolveUserId(user, out var userId)) return Results.Unauthorized();
        await mediator.Send(new CancelOrderCommand(id, userId), ct);
        return Results.NoContent();
    }

    private static bool TryResolveUserId(ClaimsPrincipal user, out UserId userId)
    {
        userId = default!;
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (sub is null || !Guid.TryParse(sub, out var guid)) return false;
        userId = new UserId(guid);
        return true;
    }
}
```

- [ ] **Step 2: Register OrderEndpoints in Program.cs**

Open `src/ECommerce.API/Program.cs` and add `app.MapOrderEndpoints();` after `app.MapCartEndpoints();`:

```csharp
// Before:
app.MapAuthEndpoints();
app.MapProductEndpoints();
app.MapCartEndpoints();
app.MapHealthEndpoints();

// After:
app.MapAuthEndpoints();
app.MapProductEndpoints();
app.MapCartEndpoints();
app.MapOrderEndpoints();
app.MapHealthEndpoints();
```

- [ ] **Step 3: Build full solution**

```bash
dotnet build
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Smoke test with docker compose (optional but recommended)**

```bash
docker compose up --build -d
```

Wait ~30s, then:

```bash
curl -s http://localhost:5000/health
```

Expected: `Healthy` or similar JSON. If the API fails to start, check logs: `docker compose logs api`.

- [ ] **Step 5: Commit**

```bash
git add src/ECommerce.API/Endpoints/OrderEndpoints.cs \
        src/ECommerce.API/Program.cs
git commit -m "feat: add OrderEndpoints and register in Program.cs"
```

---

## Task 12: Integration Tests — OrderTests.cs

**Files:**
- Create: `tests/ECommerce.IntegrationTests/Orders/OrderTests.cs`

This covers design.md integration tests 7–11 (order happy path + edge cases).

- [ ] **Step 1: Create OrderTests.cs**

```csharp
// tests/ECommerce.IntegrationTests/Orders/OrderTests.cs
using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.Common.Dtos;
using ECommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.IntegrationTests.Orders;

[Collection("Orders")]
public sealed class OrderTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private readonly Guid _userId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    private HttpClient UserClient() => factory.CreateAuthenticatedClient(_userId.ToString());
    private HttpClient AdminClient() => factory.CreateAuthenticatedClient(AdminId.ToString(), "Admin");

    // Creates a product and returns its ID
    private async Task<Guid> CreateProductAsync(bool active = true, decimal price = 9.99m)
    {
        var admin = AdminClient();
        var name = $"Product-{Guid.NewGuid():N}";
        var response = await admin.PostAsJsonAsync("/api/products", new
        {
            Name = name,
            Description = "Test product",
            Price = price,
            Stock = 100,
        });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        var id = result!["id"];

        if (!active)
        {
            var deactivate = await admin.PatchAsync($"/api/products/{id}/deactivate", null);
            deactivate.EnsureSuccessStatusCode();
        }
        return id;
    }

    // Adds a product to the user's cart
    private async Task AddToCartAsync(Guid productId, int quantity = 1)
    {
        var client = UserClient();
        var response = await client.PostAsJsonAsync("/api/cart/items", new { ProductId = productId, Quantity = quantity });
        response.EnsureSuccessStatusCode();
    }

    // Places an order and returns the orderId
    private async Task<Guid> PlaceOrderAsync()
    {
        var client = UserClient();
        var response = await client.PostAsync("/api/orders", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        return result!["orderId"];
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrder_WithItemsInCart_Returns201AndClearsCart()
    {
        var productId = await CreateProductAsync(price: 25.00m);
        await AddToCartAsync(productId, 2);
        var client = UserClient();

        var response = await client.PostAsync("/api/orders", null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        result!["orderId"].Should().NotBeEmpty();

        // Cart must be cleared
        var cart = await client.GetFromJsonAsync<CartDto>("/api/cart");
        cart!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrders_AfterPlacingOrder_ReturnsOrderList()
    {
        var productId = await CreateProductAsync(price: 10.00m);
        await AddToCartAsync(productId);
        await PlaceOrderAsync();
        var client = UserClient();

        var response = await client.GetAsync("/api/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var orders = await response.Content.ReadFromJsonAsync<List<OrderSummaryDto>>();
        orders.Should().HaveCountGreaterThanOrEqualTo(1);
        orders![0].Status.Should().Be("Pending");
        orders[0].Total.Should().Be(10.00m);
        orders[0].Items.Should().HaveCount(1);
        orders[0].Items[0].Quantity.Should().Be(1);
    }

    [Fact]
    public async Task GetOrderById_OwnOrder_ReturnsOrderDetail()
    {
        var productId = await CreateProductAsync(price: 15.00m);
        await AddToCartAsync(productId, 3);
        var orderId = await PlaceOrderAsync();
        var client = UserClient();

        var response = await client.GetAsync($"/api/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = await response.Content.ReadFromJsonAsync<OrderDetailDto>();
        order!.Id.Should().Be(orderId);
        order.Status.Should().Be("Pending");
        order.Total.Should().Be(45.00m);
        order.CancelledAt.Should().BeNull();
        order.Items.Should().HaveCount(1);
        order.Items[0].Quantity.Should().Be(3);
    }

    [Fact]
    public async Task CancelOrder_PendingOrder_Returns204AndStatusIsCancelled()
    {
        var productId = await CreateProductAsync();
        await AddToCartAsync(productId);
        var orderId = await PlaceOrderAsync();
        var client = UserClient();

        var cancelResponse = await client.PatchAsync($"/api/orders/{orderId}/cancel", null);

        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await client.GetFromJsonAsync<OrderDetailDto>($"/api/orders/{orderId}");
        detail!.Status.Should().Be("Cancelled");
        detail.CancelledAt.Should().NotBeNull();
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrder_WithEmptyCart_Returns400()
    {
        // This user has never added anything to their cart
        var client = UserClient();

        var response = await client.PostAsync("/api/orders", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        problem!["title"].ToString().Should().Contain("Cart is empty");
    }

    [Fact]
    public async Task PlaceOrder_WithInactiveProduct_Returns400()
    {
        var productId = await CreateProductAsync(active: true);
        await AddToCartAsync(productId);
        // Deactivate after adding to cart
        await AdminClient().PatchAsync($"/api/products/{productId}/deactivate", null);
        var client = UserClient();

        var response = await client.PostAsync("/api/orders", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        problem!["title"].ToString().Should().Contain(productId.ToString());
    }

    [Fact]
    public async Task GetOrderById_WrongUserJwt_Returns404()
    {
        // UserA places an order
        var productId = await CreateProductAsync();
        await AddToCartAsync(productId);
        var orderId = await PlaceOrderAsync();

        // UserB tries to access it
        var userB = factory.CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var response = await userB.GetAsync($"/api/orders/{orderId}");

        // 404 — never 403, do not leak that the order ID exists
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelOrder_AlreadyCompleted_Returns400()
    {
        var productId = await CreateProductAsync();
        await AddToCartAsync(productId);
        var orderId = await PlaceOrderAsync();

        // Force the order to Completed status directly via DB
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Orders\" SET \"Status\" = 'Completed' WHERE \"Id\" = {orderId}");

        var client = UserClient();
        var response = await client.PatchAsync($"/api/orders/{orderId}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

- [ ] **Step 2: Build tests**

```bash
dotnet build tests/ECommerce.IntegrationTests
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Run OrderTests only**

```bash
dotnet test tests/ECommerce.IntegrationTests --filter "FullyQualifiedName~OrderTests" --logger "console;verbosity=normal"
```

Expected: 8 tests pass. First run pulls Docker image (~60-90s). If tests fail, read error output — most common issues:

- `PlaceOrder_WithInactiveProduct_Returns400` fails: verify `Deactivate` endpoint path matches `ProductEndpoints.cs` (`PATCH /api/products/{id}/deactivate`)
- `CancelOrder_AlreadyCompleted_Returns400` fails: verify the table name in raw SQL matches EF's pluralised table name (`"Orders"`)
- `GetOrderById_OwnOrder_ReturnsOrderDetail` total assertion fails: verify `Money.Of` rounding in `Order.PlaceOrder` matches expected `45.00m`

- [ ] **Step 4: Commit**

```bash
git add tests/ECommerce.IntegrationTests/Orders/OrderTests.cs
git commit -m "test: add OrderTests — 8 integration tests for orders"
```

---

## Task 13: Integration Tests — AuthTests.cs + ProductTests.cs

These cover design.md tests 1–4, 12–13 (the remaining required tests not covered by CartTests).

**Files:**
- Create: `tests/ECommerce.IntegrationTests/Auth/AuthTests.cs`
- Create: `tests/ECommerce.IntegrationTests/Products/ProductTests.cs`

- [ ] **Step 1: Create AuthTests.cs**

Uses its own `[Collection("Auth")]` to get a fresh AppFactory with a fresh rate-limiter state — isolates the 429 test.

```csharp
// tests/ECommerce.IntegrationTests/Auth/AuthTests.cs
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace ECommerce.IntegrationTests.Auth;

[Collection("Auth")]
public sealed class AuthTests(AppFactory factory) : IClassFixture<AppFactory>
{
    // design.md test 1: POST /api/auth/register → 201
    [Fact]
    public async Task Register_WithValidCredentials_Returns201WithUserId()
    {
        var client = factory.CreateClient();
        var email = $"test-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "Test123!",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        result!.Should().ContainKey("id");
        result["id"].Should().NotBeEmpty();
    }

    // design.md test 2: POST /api/auth/login → JWT token
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsJwtToken()
    {
        var client = factory.CreateClient();
        var email = $"logintest-{Guid.NewGuid():N}@example.com";

        // Register first
        await client.PostAsJsonAsync("/api/auth/register", new { Email = email, Password = "Test123!" });

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "Test123!",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        result!.Should().ContainKey("token");
        result["token"].ToString().Should().NotBeNullOrEmpty();
        result.Should().ContainKey("expiresAt");
    }

    // design.md test 12: POST /api/auth/login ×6 → 6th returns 429
    [Fact]
    public async Task Login_SixTimesInOneMinute_Returns429OnSixthAttempt()
    {
        var client = factory.CreateClient();

        // Rate limit is 5/minute — make 5 requests (all will fail with 401 since user doesn't exist, but that's fine)
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new
            {
                Email = "nonexistent@example.com",
                Password = "Wrong123!",
            });
        }

        // 6th request must be rate-limited
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "nonexistent@example.com",
            Password = "Wrong123!",
        });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
```

- [ ] **Step 2: Create ProductTests.cs**

```csharp
// tests/ECommerce.IntegrationTests/Products/ProductTests.cs
using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.Common.Dtos;
using FluentAssertions;

namespace ECommerce.IntegrationTests.Products;

[Collection("Products")]
public sealed class ProductTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private HttpClient AdminClient() => factory.CreateAuthenticatedClient(AdminId.ToString(), "Admin");
    private HttpClient UserClient() => factory.CreateAuthenticatedClient(UserId.ToString(), "User");

    // design.md test 3: GET /api/products → paginated product list
    [Fact]
    public async Task GetProducts_ReturnsSeededProductList()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();
        products.Should().NotBeEmpty();
        products![0].Name.Should().NotBeNullOrEmpty();
        products[0].Price.Should().BeGreaterThan(0);
    }

    // design.md test 4: POST /api/products (admin) → 201
    [Fact]
    public async Task CreateProduct_WithAdminJwt_Returns201WithProductId()
    {
        var admin = AdminClient();

        var response = await admin.PostAsJsonAsync("/api/products", new
        {
            Name = $"TestProduct-{Guid.NewGuid():N}",
            Description = "Integration test product",
            Price = 49.99m,
            Stock = 10,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        result!["id"].Should().NotBeEmpty();
    }

    // design.md test 13: POST /api/products with user JWT → 403
    [Fact]
    public async Task CreateProduct_WithUserJwt_Returns403()
    {
        var user = UserClient();

        var response = await user.PostAsJsonAsync("/api/products", new
        {
            Name = "UnauthorizedProduct",
            Description = "Should not be created",
            Price = 9.99m,
            Stock = 5,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
```

- [ ] **Step 3: Build tests**

```bash
dotnet build tests/ECommerce.IntegrationTests
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run Auth and Product tests**

```bash
dotnet test tests/ECommerce.IntegrationTests \
  --filter "FullyQualifiedName~AuthTests|FullyQualifiedName~ProductTests" \
  --logger "console;verbosity=normal"
```

Expected: 6 tests pass (register, login, rate-limit, get-products, create-product-admin, create-product-user).

If `Login_SixTimesInOneMinute_Returns429OnSixthAttempt` fails with 401 instead of 429, the rate limiter state is being shared. Verify that `AuthTests` is in a separate `[Collection("Auth")]` from other test classes — each collection gets its own `AppFactory` instance with a fresh rate limiter.

- [ ] **Step 5: Commit**

```bash
git add tests/ECommerce.IntegrationTests/Auth/AuthTests.cs \
        tests/ECommerce.IntegrationTests/Products/ProductTests.cs
git commit -m "test: add AuthTests and ProductTests — covers design.md tests 1-4, 12-13"
```

---

## Task 14: Full Test Suite + Day 4 Gate Verification

- [ ] **Step 1: Run all tests**

```bash
dotnet test --logger "console;verbosity=normal"
```

Expected: All tests green. Count should include:
- CartTests: ~19 tests
- OrderTests: 8 tests  
- AuthTests: 3 tests
- ProductTests: 3 tests

- [ ] **Step 2: Verify the 13 design.md required tests are covered**

Skim this checklist against test output:

| # | Required Test | Test Method |
|---|--------------|-------------|
| 1 | POST /api/auth/register → 201 | `AuthTests.Register_WithValidCredentials_Returns201WithUserId` |
| 2 | POST /api/auth/login → JWT | `AuthTests.Login_WithValidCredentials_ReturnsJwtToken` |
| 3 | GET /api/products → paginated | `ProductTests.GetProducts_ReturnsSeededProductList` |
| 4 | POST /api/products (admin) → 201 | `ProductTests.CreateProduct_WithAdminJwt_Returns201WithProductId` |
| 5 | POST /api/cart/items → adds item | `CartTests.AddItem_NewCart_Returns204AndCreatesCart` |
| 6 | GET /api/cart → returns items | `CartTests.GetCart_WhenNoCartExists_Returns200WithNullCartId` (+ add item tests) |
| 7 | POST /api/orders → 201 + cart cleared | `OrderTests.PlaceOrder_WithItemsInCart_Returns201AndClearsCart` |
| 8 | GET /api/orders → order list | `OrderTests.GetOrders_AfterPlacingOrder_ReturnsOrderList` |
| 9 | POST /api/orders empty cart → 400 | `OrderTests.PlaceOrder_WithEmptyCart_Returns400` |
| 10 | POST /api/orders inactive product → 400 | `OrderTests.PlaceOrder_WithInactiveProduct_Returns400` |
| 11 | GET /api/orders/{id} wrong user → 404 | `OrderTests.GetOrderById_WrongUserJwt_Returns404` |
| 12 | POST /api/auth/login ×6 → 429 | `AuthTests.Login_SixTimesInOneMinute_Returns429OnSixthAttempt` |
| 13 | POST /api/products user JWT → 403 | `ProductTests.CreateProduct_WithUserJwt_Returns403` |

All 13 must be present and green.

- [ ] **Step 3: Verify docker compose**

```bash
docker compose up --build -d
```

Wait 30s, then:

```bash
curl -s http://localhost:5000/health
# Expected: {"status":"Healthy",...}

curl -s http://localhost:5000/api/products
# Expected: JSON array of products

docker compose down
```

If API fails to start, check: `docker compose logs api`. Most likely cause: migration fails on fresh DB. Verify Polly retry logic in `DependencyInjection.MigrateAndSeedAsync`.

- [ ] **Step 4: Final commit**

```bash
git add .
git commit -m "feat: Day 4 complete — Order aggregate, endpoints, 13 integration tests green"
```

---

## Troubleshooting

**EF migration fails at runtime:**
The migration runs via `db.Database.MigrateAsync()` on startup. If it fails, check:
- Docker postgres is healthy (`docker compose ps`)
- Migration file contains correct `Orders` and `OrderItems` table creation

**`ProductDtos.ProductDto` missing fields:**
`ProductTests` uses `ProductDto`. Check `src/ECommerce.Application/Common/Dtos/ProductDtos.cs` for its exact field names. If `Price` is named differently (e.g., `UnitPrice`), update the test assertion.

**Rate limit test flakiness:**
`Login_SixTimesInOneMinute_Returns429OnSixthAttempt` relies on a fresh `AppFactory` per test collection. If it's sharing state with other tests, the rate limiter window may already be exhausted. Ensure `AuthTests` uses `[Collection("Auth")]` and is the only class in that collection.

**`CancelOrder_AlreadyCompleted_Returns400` fails:**
The raw SQL `UPDATE "Orders" SET "Status" = 'Completed'` requires the table name to match EF's mapping. The config uses `builder.ToTable("Orders")` — verify this. If EF uses a different casing, adjust the SQL.

**`OrderAlreadyCompletedException` maps to 400:**
It extends `DomainException` (not `NotFoundException`). `ExceptionMiddleware` catches `DomainException` → 400. This is correct — verify the exception hierarchy if 500 is returned instead.
