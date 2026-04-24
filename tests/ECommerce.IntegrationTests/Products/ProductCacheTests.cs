// tests/ECommerce.IntegrationTests/Products/ProductCacheTests.cs
using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.Common.Dtos;
using FluentAssertions;

namespace ECommerce.IntegrationTests.Products;

[Collection("ProductCache")]
public sealed class ProductCacheTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private static readonly Guid AdminId = Guid.NewGuid();
    private HttpClient AdminClient() => factory.CreateAuthenticatedClient(AdminId.ToString(), "Admin");

    [Fact]
    public async Task GetProducts_SecondCall_ReturnsSameData()
    {
        var client = factory.CreateClient();

        var first = await client.GetFromJsonAsync<List<ProductDto>>("/api/products");
        var second = await client.GetFromJsonAsync<List<ProductDto>>("/api/products");

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        second!.Count.Should().Be(first!.Count);
    }

    [Fact]
    public async Task CreateProduct_ThenGetProducts_ReturnsNewProduct()
    {
        var admin = AdminClient();
        var uniqueName = $"CacheTest-{Guid.NewGuid():N}";

        var create = await admin.PostAsJsonAsync("/api/products", new
        {
            Name = uniqueName,
            Description = "Cache invalidation test",
            Price = 9.99m,
            Stock = 5,
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var products = await factory.CreateClient().GetFromJsonAsync<List<ProductDto>>("/api/products");

        products.Should().Contain(p => p.Name == uniqueName);
    }

    [Fact]
    public async Task UpdateProduct_ThenGetProductById_ReturnsUpdatedData()
    {
        var admin = AdminClient();

        var create = await admin.PostAsJsonAsync("/api/products", new
        {
            Name = $"Before-{Guid.NewGuid():N}",
            Description = "Original",
            Price = 10.00m,
            Stock = 1,
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        var id = created!["id"];

        var firstGet = await factory.CreateClient().GetFromJsonAsync<ProductDto>($"/api/products/{id}");
        firstGet.Should().NotBeNull();

        await admin.PutAsJsonAsync($"/api/products/{id}", new
        {
            Name = "After-Update",
            Description = "Updated",
            Price = 20.00m,
            Stock = 2,
        });

        var secondGet = await factory.CreateClient().GetFromJsonAsync<ProductDto>($"/api/products/{id}");
        secondGet!.Name.Should().Be("After-Update");
        secondGet.Price.Should().Be(20.00m);
    }
}
