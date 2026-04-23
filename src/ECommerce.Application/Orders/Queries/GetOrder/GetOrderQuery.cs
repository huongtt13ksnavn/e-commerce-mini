using ECommerce.Application.Common.Dtos;
using ECommerce.Domain.ValueObjects;
using MediatR;

namespace ECommerce.Application.Orders.Queries.GetOrder;

public sealed record GetOrderQuery(Guid OrderId, UserId UserId) : IRequest<OrderDetailDto>;
