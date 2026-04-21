using ECommerce.Application.Common.Dtos;
using MediatR;

namespace ECommerce.Application.Auth.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler
    : IRequestHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    public Task<CurrentUserResponse> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(new CurrentUserResponse(request.UserId, request.Email, request.Role));
}
