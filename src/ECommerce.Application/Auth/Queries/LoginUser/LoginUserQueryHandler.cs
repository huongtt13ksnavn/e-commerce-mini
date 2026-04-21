using ECommerce.Application.Common.Dtos;
using ECommerce.Domain.Exceptions;
using MediatR;

namespace ECommerce.Application.Auth.Queries.LoginUser;

public sealed class LoginUserQueryHandler(IUserService userService, IJwtTokenGenerator tokenGenerator)
    : IRequestHandler<LoginUserQuery, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginUserQuery request, CancellationToken cancellationToken)
    {
        var result = await userService.ValidateCredentialsAsync(request.Email, request.Password, cancellationToken);

        if (result is null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        var (token, expiresAt) = tokenGenerator.Generate(result.Value.UserId, result.Value.Email, result.Value.Role);
        return new LoginResponse(token, expiresAt);
    }
}
