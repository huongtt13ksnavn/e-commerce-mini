using ECommerce.Application.Cart.Commands.AddCartItem;
using ECommerce.Application.Cart.Commands.ClearCart;
using ECommerce.Application.Cart.Commands.RemoveCartItem;
using ECommerce.Application.Cart.Queries.GetCart;
using ECommerce.Application.Common.Dtos;
using ECommerce.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ECommerce.API.Endpoints;

public static class CartEndpoints
{
    public static void MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/cart", GetCartAsync)
            .WithTags("Cart")
            .RequireAuthorization()
            .Produces<CartDto>()
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/api/cart/items", AddItemAsync)
            .WithTags("Cart")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapDelete("/api/cart/items/{productId}", RemoveItemAsync)
            .WithTags("Cart")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapDelete("/api/cart", ClearCartAsync)
            .WithTags("Cart")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> GetCartAsync(ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        if (!TryResolveUserId(user, out var userId)) return Results.Unauthorized();
        var cart = await mediator.Send(new GetCartQuery(userId), ct);
        return Results.Ok(cart);
    }

    private static async Task<IResult> AddItemAsync(
        AddCartItemRequest request,
        ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!TryResolveUserId(user, out var userId)) return Results.Unauthorized();
        await mediator.Send(new AddCartItemCommand(userId, request.ProductId, request.Quantity), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveItemAsync(
        Guid productId,
        ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!TryResolveUserId(user, out var userId)) return Results.Unauthorized();
        await mediator.Send(new RemoveCartItemCommand(userId, productId), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ClearCartAsync(ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        if (!TryResolveUserId(user, out var userId)) return Results.Unauthorized();
        await mediator.Send(new ClearCartCommand(userId), ct);
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
