namespace ECommerce.Domain;

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
