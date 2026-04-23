namespace ECommerce.Domain.Exceptions;

public sealed class OrderAlreadyCompletedException()
    : DomainException("Order is already completed and cannot be cancelled.");
