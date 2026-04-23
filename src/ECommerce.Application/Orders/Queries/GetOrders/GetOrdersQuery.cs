using ECommerce.Application.Common.Dtos;
using ECommerce.Domain.ValueObjects;
using MediatR;

namespace ECommerce.Application.Orders.Queries.GetOrders;

public sealed record GetOrdersQuery(UserId UserId) : IRequest<IReadOnlyList<OrderSummaryDto>>;
