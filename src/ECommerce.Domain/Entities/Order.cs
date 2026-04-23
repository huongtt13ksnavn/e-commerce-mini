using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Events;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Entities;

public sealed class Order : AggregateRoot
{
    public Guid Id { get; private set; }
    public UserId UserId { get; private set; } = null!;
    public OrderStatus Status { get; private set; }
    public Money Total { get; private set; } = null!;
    public DateTime PlacedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    private List<OrderItem> _items = [];
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    private Order() { }

    public static Order PlaceOrder(UserId userId, IEnumerable<OrderItemData> items)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0)
            throw new CartEmptyException();

        var currency = itemList[0].UnitPrice.Currency;
        var totalAmount = itemList.Sum(i => i.UnitPrice.Amount * i.Quantity);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = OrderStatus.Pending,
            Total = Money.Of(totalAmount, currency),
            PlacedAt = DateTime.UtcNow,
            _items = itemList
                .Select(i => OrderItem.Create(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice))
                .ToList(),
        };

        order.RaiseDomainEvent(new OrderPlaced(order.Id));
        return order;
    }

    public void Cancel()
    {
        if (Status == OrderStatus.Completed)
            throw new OrderAlreadyCompletedException();
        if (Status == OrderStatus.Cancelled)
            return;
        Status = OrderStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
    }
}
