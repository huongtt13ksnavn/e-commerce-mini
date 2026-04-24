using ECommerce.Application.Caching;
using MediatR;

namespace ECommerce.Application.Products.Commands.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int Stock,
    string? ImageUrl) : IRequest, ICacheInvalidator
{
    public IReadOnlyList<string> CacheKeys => ["products:all", $"products:{Id}"];
}
