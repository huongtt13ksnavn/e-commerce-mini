using ECommerce.Domain;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.Repositories;
using MediatR;

namespace ECommerce.Application.Orders.Commands.PlaceOrder;

public sealed class PlaceOrderCommandHandler(
    ICartRepository cartRepository,
    IProductRepository productRepository,
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<PlaceOrderCommand, Guid>
{
    public async Task<Guid> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var cart = await cartRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
            throw new CartEmptyException();

        var orderItems = new List<OrderItemData>();
        foreach (var cartItem in cart.Items)
        {
            var product = await productRepository.GetByIdAsync(cartItem.ProductId, cancellationToken)
                ?? throw new NotFoundException("Product", cartItem.ProductId);
            if (!product.IsActive)
                throw new ProductUnavailableException(product.Id);
            orderItems.Add(new OrderItemData(product.Id, product.Name, cartItem.Quantity, cartItem.UnitPrice));
        }

        var order = Order.PlaceOrder(request.UserId, orderItems);
        orderRepository.Add(order);
        cart.Clear();
        await unitOfWork.CommitAsync(cancellationToken);
        return order.Id;
    }
}
