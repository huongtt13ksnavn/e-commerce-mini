using ECommerce.Application.Common.Dtos;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.Repositories;
using MediatR;

namespace ECommerce.Application.Orders.Queries.GetOrder;

public sealed class GetOrderQueryHandler(IOrderRepository orderRepository)
    : IRequestHandler<GetOrderQuery, OrderDetailDto>
{
    public async Task<OrderDetailDto> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, request.UserId, cancellationToken)
            ?? throw new OrderNotFoundException();

        return new OrderDetailDto(
            order.Id,
            order.Status.ToString(),
            order.Total.Amount,
            order.Total.Currency,
            order.PlacedAt,
            order.CancelledAt,
            order.Items
                .Select(i => new OrderItemDto(i.ProductName, i.Quantity, i.UnitPrice.Amount, i.UnitPrice.Currency))
                .ToList()
                .AsReadOnly());
    }
}
