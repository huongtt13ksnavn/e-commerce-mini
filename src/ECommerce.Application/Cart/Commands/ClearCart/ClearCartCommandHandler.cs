using ECommerce.Domain;
using ECommerce.Domain.Repositories;
using MediatR;

namespace ECommerce.Application.Cart.Commands.ClearCart;

public sealed class ClearCartCommandHandler(
    ICartRepository cartRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ClearCartCommand>
{
    public async Task Handle(ClearCartCommand request, CancellationToken cancellationToken)
    {
        var cart = await cartRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (cart is null || !cart.Items.Any()) return;

        cart.Clear();
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
