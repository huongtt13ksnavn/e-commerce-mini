using ECommerce.Application.Common.Dtos;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.Repositories;
using MediatR;

namespace ECommerce.Application.Products.Queries.GetProduct;

public sealed class GetProductQueryHandler(IProductRepository productRepository)
    : IRequestHandler<GetProductQuery, ProductDto>
{
    public async Task<ProductDto> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Product", request.Id);

        return new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Price.Amount,
            product.Price.Currency,
            product.Stock,
            product.ImageUrl,
            product.IsActive);
    }
}
