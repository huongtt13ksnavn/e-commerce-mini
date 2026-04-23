using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(o => o.PlacedAt).IsRequired();
        builder.Property(o => o.CancelledAt);

        builder.OwnsOne(o => o.Total, total =>
        {
            total.Property(m => m.Amount)
                .HasColumnName("TotalAmount")
                .HasPrecision(18, 2)
                .IsRequired();
            total.Property(m => m.Currency)
                .HasColumnName("TotalCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsMany(o => o.Items, item =>
        {
            item.ToTable("OrderItems");
            item.Property<int>("Id").ValueGeneratedOnAdd();
            item.HasKey("Id");
            item.WithOwner().HasForeignKey("OrderId");

            item.Property(i => i.ProductId).IsRequired();
            item.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
            item.Property(i => i.Quantity).IsRequired();

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

        builder.Navigation(o => o.Items).HasField("_items");
        builder.HasIndex(o => o.UserId).HasDatabaseName("ix_orders_user_id");
    }
}
