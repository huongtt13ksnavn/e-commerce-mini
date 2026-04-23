namespace ECommerce.Application.Common.Dtos;

public sealed record OrderItemDto(
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    string Currency);

public sealed record OrderSummaryDto(
    Guid OrderId,
    string Status,
    decimal Total,
    string Currency,
    DateTime PlacedAt,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderDetailDto(
    Guid Id,
    string Status,
    decimal Total,
    string Currency,
    DateTime PlacedAt,
    DateTime? CancelledAt,
    IReadOnlyList<OrderItemDto> Items);
