using ECommerce.Domain.Common;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Entities;

public sealed class Cart : AggregateRoot
{
    public Guid Id { get; private set; }
    public UserId UserId { get; private set; } = null!;

    private List<CartItem> _items = [];
    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();

    private Cart() { }

    public static Cart Create(UserId userId) => new() { Id = Guid.NewGuid(), UserId = userId };

    public void AddItem(Guid productId, int quantity, Money unitPrice)
    {
        var existing = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is not null)
            existing.IncreaseQuantity(quantity, unitPrice);
        else
            _items.Add(CartItem.Create(productId, quantity, unitPrice));
    }

    public void RemoveItem(Guid productId)
    {
        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item is not null) _items.Remove(item);
    }

    public void Clear() => _items.Clear();
}
