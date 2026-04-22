using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Exceptions;

public sealed class CartNotFoundException(UserId userId)
    : NotFoundException("Cart", userId.Value);
