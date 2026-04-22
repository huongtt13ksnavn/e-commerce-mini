using ECommerce.Application.Common.Dtos;
using MediatR;

namespace ECommerce.Application.Products.Queries.GetProduct;

public sealed record GetProductQuery(Guid Id) : IRequest<ProductDto>;
