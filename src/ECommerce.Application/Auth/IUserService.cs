namespace ECommerce.Application.Auth;

public interface IUserService
{
    Task<Guid> RegisterAsync(string email, string password, CancellationToken ct = default);
    Task<(Guid UserId, string Email, string Role)?> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);
}
