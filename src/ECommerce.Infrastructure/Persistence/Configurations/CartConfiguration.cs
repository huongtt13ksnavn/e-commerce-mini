using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .IsRequired();

        builder.HasIndex(c => c.UserId).IsUnique();

        builder.OwnsMany(c => c.Items, item =>
        {
            item.ToTable("CartItems");
            item.WithOwner().HasForeignKey("CartId");
            item.HasKey("CartId", nameof(CartItem.ProductId));

            item.Property(i => i.ProductId).IsRequired();
            item.Property(i => i.Quantity).IsRequired();

            item.HasOne<Product>().WithMany()
                .HasForeignKey(nameof(CartItem.ProductId))
                .OnDelete(DeleteBehavior.Restrict);

            item.OwnsOne(i => i.UnitPrice, price =>
            {
                price.Property(m => m.Amount)
                    .HasColumnName("UnitPrice")
                    .HasPrecision(18, 2)
                    .IsRequired();
                price.Property(m => m.Currency)
                    .HasColumnName("Currency")
                    .HasMaxLength(3)
                    .IsRequired();
            });
        });

        builder.Navigation(c => c.Items).HasField("_items");
    }
}
