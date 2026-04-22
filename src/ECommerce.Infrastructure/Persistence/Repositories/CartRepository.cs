using ECommerce.Domain.Entities;
using ECommerce.Domain.Repositories;
using ECommerce.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

public sealed class CartRepository(AppDbContext dbContext) : ICartRepository
{
    public Task<Cart?> GetByUserIdAsync(UserId userId, CancellationToken ct = default) =>
        dbContext.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

    public void Add(Cart cart) => dbContext.Carts.Add(cart);
}
