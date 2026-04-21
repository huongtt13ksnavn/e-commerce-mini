using Microsoft.AspNetCore.Identity;

namespace ECommerce.Infrastructure.Identity;

public sealed class AppUser : IdentityUser
{
    public static AppUser Create(string email)
    {
        var user = new AppUser { UserName = email, Email = email };
        return user;
    }
}
