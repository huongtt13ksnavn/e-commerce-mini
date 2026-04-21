using ECommerce.Application.Common.Dtos;
using MediatR;

namespace ECommerce.Application.Auth.Queries.LoginUser;

public sealed record LoginUserQuery(string Email, string Password) : IRequest<LoginResponse>;
