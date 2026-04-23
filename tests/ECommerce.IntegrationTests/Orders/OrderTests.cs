using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.Common.Dtos;
using ECommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.IntegrationTests.Orders;

[Collection("Orders")]
public sealed class OrderTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private readonly Guid _userId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    private HttpClient UserClient() => factory.CreateAuthenticatedClient(_userId.ToString());
    private HttpClient AdminClient() => factory.CreateAuthenticatedClient(AdminId.ToString(), "Admin");

    // Creates a product and returns its ID
    private async Task<Guid> CreateProductAsync(bool active = true, decimal price = 9.99m)
    {
        var admin = AdminClient();
        var name = $"Product-{Guid.NewGuid():N}";
        var response = await admin.PostAsJsonAsync("/api/products", new
        {
            Name = name,
            Description = "Test product",
            Price = price,
            Stock = 100,
        });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        var id = result!["id"];

        if (!active)
        {
            // DELETE /api/products/{id} deactivates the product
            var deactivate = await admin.DeleteAsync($"/api/products/{id}");
            deactivate.EnsureSuccessStatusCode();
        }
        return id;
    }

    // Adds a product to the user's cart
    private async Task AddToCartAsync(Guid productId, int quantity = 1)
    {
        var client = UserClient();
        var response = await client.PostAsJsonAsync("/api/cart/items", new { ProductId = productId, Quantity = quantity });
        response.EnsureSuccessStatusCode();
    }

    // Places an order and returns the orderId
    private async Task<Guid> PlaceOrderAsync()
    {
        var client = UserClient();
        var response = await client.PostAsync("/api/orders", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        return result!["orderId"];
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrder_WithItemsInCart_Returns201AndClearsCart()
    {
        var productId = await CreateProductAsync(price: 25.00m);
        await AddToCartAsync(productId, 2);
        var client = UserClient();

        var response = await client.PostAsync("/api/orders", null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        result!["orderId"].Should().NotBeEmpty();

        // Cart must be cleared
        var cart = await client.GetFromJsonAsync<CartDto>("/api/cart");
        cart!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrders_AfterPlacingOrder_ReturnsOrderList()
    {
        var productId = await CreateProductAsync(price: 10.00m);
        await AddToCartAsync(productId);
        await PlaceOrderAsync();
        var client = UserClient();

        var response = await client.GetAsync("/api/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var orders = await response.Content.ReadFromJsonAsync<List<OrderSummaryDto>>();
        orders.Should().HaveCountGreaterThanOrEqualTo(1);
        orders![0].Status.Should().Be("Pending");
        orders[0].Total.Should().Be(10.00m);
        orders[0].Items.Should().HaveCount(1);
        orders[0].Items[0].Quantity.Should().Be(1);
    }

    [Fact]
    public async Task GetOrderById_OwnOrder_ReturnsOrderDetail()
    {
        var productId = await CreateProductAsync(price: 15.00m);
        await AddToCartAsync(productId, 3);
        var orderId = await PlaceOrderAsync();
        var client = UserClient();

        var response = await client.GetAsync($"/api/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = await response.Content.ReadFromJsonAsync<OrderDetailDto>();
        order!.Id.Should().Be(orderId);
        order.Status.Should().Be("Pending");
        order.Total.Should().Be(45.00m);
        order.CancelledAt.Should().BeNull();
        order.Items.Should().HaveCount(1);
        order.Items[0].Quantity.Should().Be(3);
    }

    [Fact]
    public async Task CancelOrder_PendingOrder_Returns204AndStatusIsCancelled()
    {
        var productId = await CreateProductAsync();
        await AddToCartAsync(productId);
        var orderId = await PlaceOrderAsync();
        var client = UserClient();

        var cancelResponse = await client.PatchAsync($"/api/orders/{orderId}/cancel", null);

        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await client.GetFromJsonAsync<OrderDetailDto>($"/api/orders/{orderId}");
        detail!.Status.Should().Be("Cancelled");
        detail.CancelledAt.Should().NotBeNull();
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrder_WithEmptyCart_Returns400()
    {
        // This user has never added anything to their cart
        var client = UserClient();

        var response = await client.PostAsync("/api/orders", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        problem!["title"].ToString().Should().Contain("Cart is empty");
    }

    [Fact]
    public async Task PlaceOrder_WithInactiveProduct_Returns400()
    {
        var productId = await CreateProductAsync(active: true);
        await AddToCartAsync(productId);
        // Deactivate after adding to cart — DELETE /api/products/{id} deactivates
        await AdminClient().DeleteAsync($"/api/products/{productId}");
        var client = UserClient();

        var response = await client.PostAsync("/api/orders", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        problem!["title"].ToString().Should().Contain(productId.ToString());
    }

    [Fact]
    public async Task GetOrderById_WrongUserJwt_Returns404()
    {
        // UserA places an order
        var productId = await CreateProductAsync();
        await AddToCartAsync(productId);
        var orderId = await PlaceOrderAsync();

        // UserB tries to access it
        var userB = factory.CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var response = await userB.GetAsync($"/api/orders/{orderId}");

        // 404 — never 403, do not leak that the order ID exists
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelOrder_AlreadyCompleted_Returns400()
    {
        var productId = await CreateProductAsync();
        await AddToCartAsync(productId);
        var orderId = await PlaceOrderAsync();

        // Force the order to Completed status directly via DB
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE \"Orders\" SET \"Status\" = 'Completed' WHERE \"Id\" = {orderId}");

        var client = UserClient();
        var response = await client.PatchAsync($"/api/orders/{orderId}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
