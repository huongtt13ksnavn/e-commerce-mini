namespace ECommerce.Domain.ValueObjects;

public sealed record UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());
    public static UserId From(string id) =>
        Guid.TryParse(id, out var guid)
            ? new(guid)
            : throw new FormatException($"'{id}' is not a valid UserId.");
    public override string ToString() => Value.ToString();
}
