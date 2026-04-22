using ECommerce.Domain.ValueObjects;
using MediatR;

namespace ECommerce.Application.Cart.Commands.AddCartItem;

public sealed record AddCartItemCommand(UserId UserId, Guid ProductId, int Quantity) : IRequest;
