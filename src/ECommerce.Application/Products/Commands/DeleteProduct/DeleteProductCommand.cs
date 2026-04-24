using ECommerce.Application.Caching;
using MediatR;

namespace ECommerce.Application.Products.Commands.DeleteProduct;

public sealed record DeleteProductCommand(Guid Id) : IRequest, ICacheInvalidator
{
    public IReadOnlyList<string> CacheKeys => ["products:all", $"products:{Id}"];
}
