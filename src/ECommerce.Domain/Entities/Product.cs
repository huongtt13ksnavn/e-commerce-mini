using ECommerce.Domain.Common;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.ValueObjects;

namespace ECommerce.Domain.Entities;

public sealed class Product : AggregateRoot
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public Money Price { get; private set; } = null!;
    public int Stock { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool IsActive { get; private set; }

    private Product() { }

    public static Product Create(string name, string description, Money price, int stock, string? imageUrl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfNegative(stock);

        return new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Price = price,
            Stock = stock,
            ImageUrl = imageUrl,
            IsActive = true,
        };
    }

    public void Update(string name, string description, Money price, int stock, string? imageUrl)
    {
        if (!IsActive) throw new ProductUnavailableException(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfNegative(stock);

        Name = name;
        Description = description;
        Price = price;
        Stock = stock;
        if (imageUrl is not null) ImageUrl = imageUrl;
    }

    public void Deactivate() => IsActive = false;
}
