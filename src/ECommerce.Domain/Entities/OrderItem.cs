using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Entities;

public sealed class OrderItem
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = null!;

    private OrderItem() { }

    internal static OrderItem Create(Guid productId, string productName, int quantity, Money unitPrice) =>
        new() { ProductId = productId, ProductName = productName, Quantity = quantity, UnitPrice = unitPrice };
}
