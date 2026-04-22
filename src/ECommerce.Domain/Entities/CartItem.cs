using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Entities;

public sealed class CartItem
{
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = null!;

    private CartItem() { }

    internal static CartItem Create(Guid productId, int quantity, Money unitPrice)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        return new CartItem { ProductId = productId, Quantity = quantity, UnitPrice = unitPrice };
    }

    internal void IncreaseQuantity(int by, Money newUnitPrice)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(by);
        if (Quantity + by > 1000)
            throw new ArgumentOutOfRangeException(nameof(by), "Total quantity per item cannot exceed 1000.");
        Quantity += by;
        UnitPrice = newUnitPrice;
    }
}
