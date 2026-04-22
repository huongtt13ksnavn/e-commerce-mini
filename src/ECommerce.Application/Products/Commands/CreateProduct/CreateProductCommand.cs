using MediatR;

namespace ECommerce.Application.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    string Description,
    decimal Price,
    int Stock,
    string? ImageUrl) : IRequest<Guid>;
