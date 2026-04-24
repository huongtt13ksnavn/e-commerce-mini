using ECommerce.Application.Caching;
using ECommerce.Application.Common.Dtos;
using MediatR;

namespace ECommerce.Application.Products.Queries.GetProduct;

public sealed record GetProductQuery(Guid Id) : IRequest<ProductDto>, ICacheable
{
    public string CacheKey => $"products:{Id}";
    public TimeSpan CacheDuration => TimeSpan.FromMinutes(2);
}
