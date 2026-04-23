using ECommerce.Domain.Common;

namespace ECommerce.Domain.Events;

public record OrderPlaced(Guid OrderId) : IDomainEvent;
