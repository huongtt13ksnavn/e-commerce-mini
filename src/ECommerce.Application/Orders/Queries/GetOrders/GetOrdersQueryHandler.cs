using ECommerce.Application.Common.Dtos;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Repositories;
using MediatR;

namespace ECommerce.Application.Orders.Queries.GetOrders;

public sealed class GetOrdersQueryHandler(IOrderRepository orderRepository)
    : IRequestHandler<GetOrdersQuery, IReadOnlyList<OrderSummaryDto>>
{
    public async Task<IReadOnlyList<OrderSummaryDto>> Handle(
        GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await orderRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        return orders.Select(MapToSummary).ToList().AsReadOnly();
    }

    private static OrderSummaryDto MapToSummary(Order o) => new(
        o.Id,
        o.Status.ToString(),
        o.Total.Amount,
        o.Total.Currency,
        o.PlacedAt,
        o.Items
            .Select(i => new OrderItemDto(i.ProductName, i.Quantity, i.UnitPrice.Amount, i.UnitPrice.Currency))
            .ToList()
            .AsReadOnly());
}
