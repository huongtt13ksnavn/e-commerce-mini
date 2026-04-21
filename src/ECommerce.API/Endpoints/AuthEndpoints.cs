using ECommerce.Application.Auth.Commands.RegisterUser;
using ECommerce.Application.Auth.Queries.GetCurrentUser;
using ECommerce.Application.Auth.Queries.LoginUser;
using ECommerce.Application.Common.Dtos;
using ECommerce.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ECommerce.API.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .Produces<object>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting("login")
            .Produces<LoginResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests);

        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization()
            .Produces<CurrentUserResponse>()
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterUserRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var userId = await mediator.Send(new RegisterUserCommand(request.Email, request.Password), ct);
        return Results.Created($"/api/users/{userId}", new { id = userId });
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var response = await mediator.Send(new LoginUserQuery(request.Email, request.Password), ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken ct)
    {
        var userId = Guid.Parse(user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!);
        var email = user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)!;
        var role = user.FindFirstValue(ClaimTypes.Role)!;

        var response = await mediator.Send(new GetCurrentUserQuery(userId, email, role), ct);
        return Results.Ok(response);
    }
}
