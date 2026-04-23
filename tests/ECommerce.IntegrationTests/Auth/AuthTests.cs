// tests/ECommerce.IntegrationTests/Auth/AuthTests.cs
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace ECommerce.IntegrationTests.Auth;

/// <summary>
/// Happy-path auth tests (design.md tests 1 and 2).
/// Kept in a separate class from the rate-limit test so each class
/// gets its own IClassFixture&lt;AppFactory&gt; — preventing the 429 test
/// from exhausting the rate-limiter shared with register/login tests.
/// </summary>
[Collection("Auth")]
public sealed class AuthTests(AppFactory factory) : IClassFixture<AppFactory>
{
    // design.md test 1: POST /api/auth/register → 201
    [Fact]
    public async Task Register_WithValidCredentials_Returns201WithUserId()
    {
        var client = factory.CreateClient();
        var email = $"test-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "Test123!",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        result!.Should().ContainKey("id");
        result["id"].Should().NotBeEmpty();
    }

    // design.md test 2: POST /api/auth/login → JWT token
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsJwtToken()
    {
        var client = factory.CreateClient();
        var email = $"logintest-{Guid.NewGuid():N}@example.com";

        // Register first
        await client.PostAsJsonAsync("/api/auth/register", new { Email = email, Password = "Test123!" });

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "Test123!",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        result!.Should().ContainKey("token");
        result["token"].ToString().Should().NotBeNullOrEmpty();
        result.Should().ContainKey("expiresAt");
    }
}

/// <summary>
/// Rate-limit test (design.md test 12).
/// Uses its own IClassFixture&lt;AppFactory&gt; so it gets a fresh server
/// (and fresh rate-limiter state) isolated from the happy-path tests.
/// </summary>
[Collection("AuthRateLimit")]
public sealed class AuthRateLimitTests(AppFactory factory) : IClassFixture<AppFactory>
{
    // design.md test 12: POST /api/auth/login ×6 → 6th returns 429
    [Fact]
    public async Task Login_SixTimesInOneMinute_Returns429OnSixthAttempt()
    {
        var client = factory.CreateClient();

        // PermitLimit = 5 per minute (Program.cs rate-limiter config); 6th request must be rejected
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new
            {
                Email = "nonexistent@example.com",
                Password = "Wrong123!",
            });
        }

        // 6th request must be rate-limited
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "nonexistent@example.com",
            Password = "Wrong123!",
        });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
