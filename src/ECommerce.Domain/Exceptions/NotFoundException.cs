namespace ECommerce.Domain.Exceptions;

public sealed class NotFoundException(string entityName, object id)
    : DomainException($"{entityName} with id '{id}' was not found.");
