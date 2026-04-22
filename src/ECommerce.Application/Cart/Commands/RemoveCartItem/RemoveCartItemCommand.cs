using ECommerce.Domain.ValueObjects;
using MediatR;

namespace ECommerce.Application.Cart.Commands.RemoveCartItem;

public sealed record RemoveCartItemCommand(UserId UserId, Guid ProductId) : IRequest;
