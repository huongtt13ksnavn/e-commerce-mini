namespace ECommerce.Domain.Exceptions;

public sealed class OrderNotFoundException() : NotFoundException("Order", string.Empty);
