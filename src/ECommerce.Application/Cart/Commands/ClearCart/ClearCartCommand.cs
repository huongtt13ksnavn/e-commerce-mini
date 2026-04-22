using ECommerce.Domain.ValueObjects;
using MediatR;

namespace ECommerce.Application.Cart.Commands.ClearCart;

public sealed record ClearCartCommand(UserId UserId) : IRequest;
