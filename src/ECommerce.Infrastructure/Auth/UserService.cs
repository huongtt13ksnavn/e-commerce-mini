using ECommerce.Application.Auth;
using ECommerce.Domain.Exceptions;
using ECommerce.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace ECommerce.Infrastructure.Auth;

public sealed class UserService(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager) : IUserService
{
    public async Task<Guid> RegisterAsync(string email, string password, CancellationToken ct = default)
    {
        var user = AppUser.Create(email);
        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new RegistrationFailedException(errors);
        }

        await userManager.AddToRoleAsync(user, "User");
        return Guid.Parse(user.Id);
    }

    public async Task<(Guid UserId, string Email, string Role)?> ValidateCredentialsAsync(
        string email, string password, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null) return null;

        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded) return null;

        var roles = await userManager.GetRolesAsync(user);
        var role = roles.Contains("Admin") ? "Admin" : "User";

        return (Guid.Parse(user.Id), user.Email!, role);
    }
}
