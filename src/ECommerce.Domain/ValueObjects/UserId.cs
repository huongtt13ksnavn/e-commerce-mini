namespace ECommerce.Domain.ValueObjects;

public sealed record UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());
    public static UserId From(string id) => new(Guid.Parse(id));
    public override string ToString() => Value.ToString();
}
