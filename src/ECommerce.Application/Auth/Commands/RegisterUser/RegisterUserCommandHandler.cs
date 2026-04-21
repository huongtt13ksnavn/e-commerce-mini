using MediatR;

namespace ECommerce.Application.Auth.Commands.RegisterUser;

public sealed class RegisterUserCommandHandler(IUserService userService)
    : IRequestHandler<RegisterUserCommand, Guid>
{
    public Task<Guid> Handle(RegisterUserCommand request, CancellationToken cancellationToken) =>
        userService.RegisterAsync(request.Email, request.Password, cancellationToken);
}
