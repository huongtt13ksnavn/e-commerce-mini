namespace ECommerce.Domain.Exceptions;

public class NotFoundException(string entityName, object id)
    : DomainException($"{entityName} with id '{id}' was not found.");
