namespace ECommerce.Application.Common.Dtos;

public sealed record RegisterUserRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string Token, DateTime ExpiresAt);

public sealed record CurrentUserResponse(Guid Id, string Email, string Role);
