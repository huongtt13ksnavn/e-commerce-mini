namespace ECommerce.Application.Auth;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) Generate(Guid userId, string email, string role);
}
