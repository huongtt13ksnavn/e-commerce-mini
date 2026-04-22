using ECommerce.Application.Common.Dtos;
using ECommerce.Domain.ValueObjects;
using MediatR;

namespace ECommerce.Application.Cart.Queries.GetCart;

public sealed record GetCartQuery(UserId UserId) : IRequest<CartDto>;
