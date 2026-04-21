namespace ECommerce.Domain.Exceptions;

public sealed class ProductUnavailableException(Guid productId)
    : DomainException($"Product '{productId}' is no longer available.");
