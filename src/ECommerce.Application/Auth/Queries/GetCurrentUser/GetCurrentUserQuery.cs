using ECommerce.Application.Common.Dtos;
using MediatR;

namespace ECommerce.Application.Auth.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery(Guid UserId, string Email, string Role) : IRequest<CurrentUserResponse>;
