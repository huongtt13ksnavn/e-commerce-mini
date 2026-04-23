// tests/ECommerce.IntegrationTests/Products/ProductTests.cs
using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.Common.Dtos;
using FluentAssertions;

namespace ECommerce.IntegrationTests.Products;

[Collection("Products")]
public sealed class ProductTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private HttpClient AdminClient() => factory.CreateAuthenticatedClient(AdminId.ToString(), "Admin");
    private HttpClient UserClient() => factory.CreateAuthenticatedClient(UserId.ToString(), "User");

    // design.md test 3: GET /api/products → paginated product list
    [Fact]
    public async Task GetProducts_ReturnsSeededProductList()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();
        products.Should().NotBeEmpty();
        products![0].Name.Should().NotBeNullOrEmpty();
        products[0].Price.Should().BeGreaterThan(0);
    }

    // design.md test 4: POST /api/products (admin) → 201
    [Fact]
    public async Task CreateProduct_WithAdminJwt_Returns201WithProductId()
    {
        var admin = AdminClient();

        var response = await admin.PostAsJsonAsync("/api/products", new
        {
            Name = $"TestProduct-{Guid.NewGuid():N}",
            Description = "Integration test product",
            Price = 49.99m,
            Stock = 10,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        result!["id"].Should().NotBeEmpty();
    }

    // design.md test 13: POST /api/products with user JWT → 403
    [Fact]
    public async Task CreateProduct_WithUserJwt_Returns403()
    {
        var user = UserClient();

        var response = await user.PostAsJsonAsync("/api/products", new
        {
            Name = "UnauthorizedProduct",
            Description = "Should not be created",
            Price = 9.99m,
            Stock = 5,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
