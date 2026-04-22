using ECommerce.Application.Common.Dtos;
using ECommerce.Application.Products.Commands.CreateProduct;
using ECommerce.Application.Products.Commands.DeleteProduct;
using ECommerce.Application.Products.Commands.UpdateProduct;
using ECommerce.Application.Products.Queries.GetProduct;
using ECommerce.Application.Products.Queries.GetProducts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products");

        group.MapGet("/", GetAllAsync)
            .AllowAnonymous()
            .Produces<IReadOnlyList<ProductDto>>();

        group.MapGet("/{id:guid}", GetByIdAsync)
            .AllowAnonymous()
            .Produces<ProductDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateAsync)
            .RequireAuthorization("AdminOnly")
            .Produces<object>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization("AdminOnly")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization("AdminOnly")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetAllAsync(IMediator mediator, CancellationToken ct)
    {
        var products = await mediator.Send(new GetProductsQuery(), ct);
        return Results.Ok(products);
    }

    private static async Task<IResult> GetByIdAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var product = await mediator.Send(new GetProductQuery(id), ct);
        return Results.Ok(product);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateProductRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var id = await mediator.Send(
            new CreateProductCommand(request.Name, request.Description, request.Price, request.Stock, request.ImageUrl),
            ct);

        return Results.Created($"/api/products/{id}", new { id });
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateProductRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        await mediator.Send(
            new UpdateProductCommand(id, request.Name, request.Description, request.Price, request.Stock, request.ImageUrl),
            ct);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        await mediator.Send(new DeleteProductCommand(id), ct);
        return Results.NoContent();
    }
}
