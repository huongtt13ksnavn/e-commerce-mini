using ECommerce.Application.Auth;
using ECommerce.Domain;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Repositories;
using ECommerce.Domain.ValueObjects;
using ECommerce.Infrastructure.Auth;
using ECommerce.Infrastructure.Identity;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Infrastructure.Persistence.Interceptors;
using ECommerce.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace ECommerce.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");

        _ = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret configuration is required.");

        services.AddSingleton<AuditInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);
            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });

        services.AddIdentity<AppUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        return services;
    }

    public static async Task MigrateAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 5,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
            })
            .Build();

        await retryPipeline.ExecuteAsync(async ct => await db.Database.MigrateAsync(ct));

        await SeedRolesAsync(roleManager);
        await SeedAdminAsync(userManager);
        await SeedProductsAsync(db);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { "Admin", "User" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private static async Task SeedAdminAsync(UserManager<AppUser> userManager)
    {
        const string adminEmail = "admin@example.com";
        const string adminPassword = "Admin123!";

        if (await userManager.FindByEmailAsync(adminEmail) is not null)
            return;

        var admin = AppUser.Create(adminEmail);
        await userManager.CreateAsync(admin, adminPassword);
        await userManager.AddToRoleAsync(admin, "Admin");
    }

    private static async Task SeedProductsAsync(AppDbContext db)
    {
        if (await db.Products.AnyAsync())
            return;

        var products = new[]
        {
            Product.Create("Wireless Noise-Cancelling Headphones", "Over-ear headphones with 30h battery and active noise cancellation.", Money.Of(149.99m), 50),
            Product.Create("Mechanical Gaming Keyboard", "Tenkeyless layout with Cherry MX Red switches and RGB backlight.", Money.Of(89.99m), 75),
            Product.Create("4K USB-C Monitor", "27-inch IPS panel, 144 Hz, with USB-C power delivery.", Money.Of(499.99m), 30),
            Product.Create("Ergonomic Office Chair", "Lumbar support, adjustable armrests, breathable mesh back.", Money.Of(299.99m), 20),
            Product.Create("Portable SSD 1TB", "USB 3.2 Gen 2 with read speeds up to 1050 MB/s.", Money.Of(109.99m), 100),
            Product.Create("Smart LED Desk Lamp", "Touch dimmer, USB-A charging port, color temperature control.", Money.Of(39.99m), 200),
            Product.Create("Webcam 1080p 60fps", "Wide-angle lens, built-in dual microphone, plug-and-play.", Money.Of(69.99m), 60),
            Product.Create("Wireless Mouse", "Ergonomic shape, 70-day battery life, silent clicks.", Money.Of(29.99m), 150),
            Product.Create("USB-C Hub 7-in-1", "HDMI 4K, 3× USB-A, SD/microSD, 100W PD passthrough.", Money.Of(49.99m), 80),
            Product.Create("Laptop Stand Adjustable", "Aluminium, 6 height levels, folds flat for travel.", Money.Of(34.99m), 120),
        };

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }
}
