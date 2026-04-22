using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.Common.Dtos;
using FluentAssertions;

namespace ECommerce.IntegrationTests.Cart;

[Collection("Cart")]
public sealed class CartTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    // Helpers ----------------------------------------------------------------

    private HttpClient UserClient() => factory.CreateAuthenticatedClient(UserId.ToString());
    private HttpClient AdminClient() => factory.CreateAuthenticatedClient(AdminId.ToString(), "Admin");

    private async Task<Guid> CreateProductAsync(string? name = null, decimal price = 9.99m)
    {
        var admin = AdminClient();
        var response = await admin.PostAsJsonAsync("/api/products", new
        {
            Name = name ?? $"Product-{Guid.NewGuid():N}",
            Description = "Test product",
            Price = price,
            Stock = 100
        });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        return result!["id"];
    }

    // Happy path -------------------------------------------------------------

    [Fact]
    public async Task GetCart_WhenNoCartExists_Returns200WithNullCartId()
    {
        var client = UserClient();
        var response = await client.GetAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<CartDto>();
        dto!.CartId.Should().BeNull();
        dto.Items.Should().BeEmpty();
        dto.Total.Should().Be(0);
    }

    [Fact]
    public async Task AddItem_NewCart_Returns204AndCreatesCart()
    {
        var productId = await CreateProductAsync("NewCartProduct");
        var client = UserClient();

        var response = await client.PostAsJsonAsync("/api/cart/items",
            new { ProductId = productId, Quantity = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var cart = await client.GetFromJsonAsync<CartDto>("/api/cart");
        cart!.CartId.Should().NotBeNull();
        cart.Items.Should().HaveCount(1);
        cart.Items[0].ProductId.Should().Be(productId);
        cart.Items[0].Quantity.Should().Be(2);
    }

    [Fact]
    public async Task AddItem_SameProductTwice_MergesQuantityAndUpdatesPrice()
    {
        var productId = await CreateProductAsync("MergeProduct", 10.00m);
        var client = UserClient();

        await client.PostAsJsonAsync("/api/cart/items", new { ProductId = productId, Quantity = 1 });

        // Admin updates price
        await AdminClient().PutAsJsonAsync($"/api/products/{productId}",
            new { Name = "MergeProduct", Description = "Test product", Price = 20.00m, Stock = 100 });

        await client.PostAsJsonAsync("/api/cart/items", new { ProductId = productId, Quantity = 2 });

        var cart = await client.GetFromJsonAsync<CartDto>("/api/cart");
        cart!.Items.Should().HaveCount(1);
        cart.Items[0].Quantity.Should().Be(3);
        cart.Items[0].UnitPrice.Should().Be(20.00m, "re-add should snapshot current price");
    }

    [Fact]
    public async Task RemoveItem_ExistingItem_Returns204AndRemovesFromCart()
    {
        var productId = await CreateProductAsync("RemoveProduct");
        var client = UserClient();
        await client.PostAsJsonAsync("/api/cart/items", new { ProductId = productId, Quantity = 1 });

        var response = await client.DeleteAsync($"/api/cart/items/{productId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cart = await client.GetFromJsonAsync<CartDto>("/api/cart");
        cart!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearCart_WithItems_Returns204AndEmptiesCart()
    {
        var productId = await CreateProductAsync("ClearProduct");
        var client = UserClient();
        await client.PostAsJsonAsync("/api/cart/items", new { ProductId = productId, Quantity = 1 });

        var response = await client.DeleteAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cart = await client.GetFromJsonAsync<CartDto>("/api/cart");
        cart!.Items.Should().BeEmpty();
    }

    // Auth enforcement -------------------------------------------------------

    [Theory]
    [InlineData("GET", "/api/cart")]
    [InlineData("POST", "/api/cart/items")]
    [InlineData("DELETE", "/api/cart/items/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/cart")]
    public async Task CartEndpoints_WithoutToken_Returns401(string method, string path)
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
            request.Content = JsonContent.Create(new { ProductId = Guid.NewGuid(), Quantity = 1 });

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Negative paths ---------------------------------------------------------

    [Fact]
    public async Task AddItem_NonExistentProduct_Returns404()
    {
        var client = UserClient();
        var response = await client.PostAsJsonAsync("/api/cart/items",
            new { ProductId = Guid.NewGuid(), Quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddItem_DeactivatedProduct_Returns400()
    {
        var productId = await CreateProductAsync("DeactivatedProduct");
        await AdminClient().DeleteAsync($"/api/products/{productId}");

        var response = await UserClient().PostAsJsonAsync("/api/cart/items",
            new { ProductId = productId, Quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AddItem_InvalidQuantity_Returns422(int quantity)
    {
        var productId = await CreateProductAsync();
        var response = await UserClient().PostAsJsonAsync("/api/cart/items",
            new { ProductId = productId, Quantity = quantity });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task AddItem_QuantityExceedsMax_Returns422()
    {
        var productId = await CreateProductAsync();
        var response = await UserClient().PostAsJsonAsync("/api/cart/items",
            new { ProductId = productId, Quantity = 1001 });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task RemoveItem_NotInCart_Returns204SilentNoOp()
    {
        var client = UserClient();
        var response = await client.DeleteAsync($"/api/cart/items/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ClearCart_NoCartExists_Returns204SilentNoOp()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var response = await client.DeleteAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
