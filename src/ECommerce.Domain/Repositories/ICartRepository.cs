using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Repositories;

public interface ICartRepository
{
    Task<Cart?> GetByUserIdAsync(UserId userId, CancellationToken ct = default);
    void Add(Cart cart);
}
