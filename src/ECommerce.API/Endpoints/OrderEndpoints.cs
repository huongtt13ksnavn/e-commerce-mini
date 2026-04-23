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
