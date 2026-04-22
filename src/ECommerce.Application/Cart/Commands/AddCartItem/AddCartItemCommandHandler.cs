using ECommerce.Domain;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.Repositories;
using MediatR;

namespace ECommerce.Application.Cart.Commands.AddCartItem;

public sealed class AddCartItemCommandHandler(
    IProductRepository productRepository,
    ICartRepository cartRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AddCartItemCommand>
{
    public async Task Handle(AddCartItemCommand request, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        if (!product.IsActive)
            throw new ProductUnavailableException(product.Id);

        var cart = await cartRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (cart is null)
        {
            cart = Domain.Entities.Cart.Create(request.UserId);
            cartRepository.Add(cart);
        }

        cart.AddItem(product.Id, request.Quantity, product.Price);
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
