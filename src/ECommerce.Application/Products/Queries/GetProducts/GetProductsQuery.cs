using ECommerce.Application.Caching;
using ECommerce.Application.Common.Dtos;
using MediatR;

namespace ECommerce.Application.Products.Queries.GetProducts;

public sealed record GetProductsQuery : IRequest<IReadOnlyList<ProductDto>>, ICacheable
{
    public string CacheKey => "products:all";
    public TimeSpan CacheDuration => TimeSpan.FromMinutes(2);
}
