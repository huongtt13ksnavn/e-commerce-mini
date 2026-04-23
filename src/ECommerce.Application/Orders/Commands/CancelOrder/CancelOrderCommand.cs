using ECommerce.Domain.ValueObjects;
using MediatR;

namespace ECommerce.Application.Orders.Commands.CancelOrder;

public sealed record CancelOrderCommand(Guid OrderId, UserId UserId) : IRequest;
