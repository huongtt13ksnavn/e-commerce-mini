using MediatR;

namespace ECommerce.Application.Auth.Commands.RegisterUser;

public sealed record RegisterUserCommand(string Email, string Password) : IRequest<Guid>;
