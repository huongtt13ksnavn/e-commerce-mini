using ECommerce.Application.Common.Dtos;
using MediatR;

namespace ECommerce.Application.Products.Queries.GetProducts;

public sealed record GetProductsQuery : IRequest<IReadOnlyList<ProductDto>>;
