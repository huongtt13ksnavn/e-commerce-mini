namespace ECommerce.Application.Common.Dtos;

public sealed record CartDto(
    Guid? CartId,
    IReadOnlyList<CartItemDto> Items,
    decimal Total,
    string Currency);

public sealed record CartItemDto(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    string Currency);

public sealed record AddCartItemRequest(Guid ProductId, int Quantity);
