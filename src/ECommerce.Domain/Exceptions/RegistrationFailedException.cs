namespace ECommerce.Domain.Exceptions;

public sealed class RegistrationFailedException(string message) : DomainException(message);
