using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace ECommerce.IntegrationTests;

public sealed class AppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:Secret", "test-secret-at-least-32-characters-long!!!");
        builder.UseSetting("Jwt:Issuer", "ECommerce.API");
        builder.UseSetting("Jwt:Audience", "ECommerce.Client");
        builder.UseEnvironment("Testing");
    }

    public HttpClient CreateAuthenticatedClient(string userId, string role = "User")
    {
        var client = CreateClient();
        var token = JwtHelper.Generate(userId, role);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }
}
