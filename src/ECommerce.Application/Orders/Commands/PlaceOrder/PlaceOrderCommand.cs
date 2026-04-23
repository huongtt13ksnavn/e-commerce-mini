using ECommerce.Domain.ValueObjects;
using MediatR;

namespace ECommerce.Application.Orders.Commands.PlaceOrder;

public sealed record PlaceOrderCommand(UserId UserId) : IRequest<Guid>;
