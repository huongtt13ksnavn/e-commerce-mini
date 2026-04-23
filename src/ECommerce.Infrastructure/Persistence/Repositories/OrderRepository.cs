using ECommerce.Domain.Entities;
using ECommerce.Domain.Repositories;
using ECommerce.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository(AppDbContext dbContext) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(Guid orderId, UserId userId, CancellationToken ct = default) =>
        dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);

    public async Task<IReadOnlyList<Order>> GetByUserIdAsync(UserId userId, CancellationToken ct = default) =>
        await dbContext.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.PlacedAt)
            .ToListAsync(ct);

    public void Add(Order order) => dbContext.Orders.Add(order);
}
