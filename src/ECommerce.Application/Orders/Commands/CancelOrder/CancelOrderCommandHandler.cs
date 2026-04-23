using ECommerce.Domain;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.Repositories;
using MediatR;

namespace ECommerce.Application.Orders.Commands.CancelOrder;

public sealed class CancelOrderCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CancelOrderCommand>
{
    public async Task Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, request.UserId, cancellationToken)
            ?? throw new OrderNotFoundException();
        order.Cancel();
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
