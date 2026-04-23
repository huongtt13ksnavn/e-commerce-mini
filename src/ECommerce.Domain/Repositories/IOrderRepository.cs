using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Repositories;

public interface IOrderRepository
{
    /// <summary>Returns null if not found OR if the order belongs to a different user.</summary>
    Task<Order?> GetByIdAsync(Guid orderId, UserId userId, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetByUserIdAsync(UserId userId, CancellationToken ct = default);
    void Add(Order order);
}
