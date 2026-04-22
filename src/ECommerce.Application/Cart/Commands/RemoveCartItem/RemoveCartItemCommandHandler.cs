using ECommerce.Domain;
using ECommerce.Domain.Repositories;
using MediatR;

namespace ECommerce.Application.Cart.Commands.RemoveCartItem;

public sealed class RemoveCartItemCommandHandler(
    ICartRepository cartRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RemoveCartItemCommand>
{
    public async Task Handle(RemoveCartItemCommand request, CancellationToken cancellationToken)
    {
        var cart = await cartRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (cart is null || !cart.Items.Any(i => i.ProductId == request.ProductId)) return;

        cart.RemoveItem(request.ProductId);
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
