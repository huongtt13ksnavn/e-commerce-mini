using ECommerce.Application.Common.Dtos;
using ECommerce.Domain.Repositories;
using MediatR;

namespace ECommerce.Application.Cart.Queries.GetCart;

public sealed class GetCartQueryHandler(ICartRepository cartRepository)
    : IRequestHandler<GetCartQuery, CartDto>
{
    private const string DefaultCurrency = "USD";

    public async Task<CartDto> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        var cart = await cartRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (cart is null)
            return new CartDto(null, [], 0m, DefaultCurrency);

        var items = cart.Items
            .Select(i => new CartItemDto(i.ProductId, i.Quantity, i.UnitPrice.Amount, i.UnitPrice.Currency))
            .ToList();

        var total = items.Sum(i => i.Quantity * i.UnitPrice);
        var currency = items.FirstOrDefault()?.Currency ?? DefaultCurrency;

        return new CartDto((Guid?)cart.Id, items, total, currency);
    }
}
