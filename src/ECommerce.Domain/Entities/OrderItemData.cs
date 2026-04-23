using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Entities;

public record OrderItemData(Guid ProductId, string ProductName, int Quantity, Money UnitPrice);
