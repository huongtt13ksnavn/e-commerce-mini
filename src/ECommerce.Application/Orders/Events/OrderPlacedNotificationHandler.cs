using ECommerce.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ECommerce.Application.Orders.Events;

public sealed class OrderPlacedNotificationHandler(ILogger<OrderPlacedNotificationHandler> logger)
    : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Order placed: {OrderId}", notification.OrderId);
        return Task.CompletedTask;
    }
}
