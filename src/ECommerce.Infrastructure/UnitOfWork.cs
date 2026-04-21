using ECommerce.Domain;
using ECommerce.Infrastructure.Persistence;

namespace ECommerce.Infrastructure;

public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
