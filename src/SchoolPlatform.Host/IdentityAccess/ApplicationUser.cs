using Microsoft.AspNetCore.Identity;

namespace SchoolPlatform.Host.IdentityAccess;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public bool IsActive { get; set; }
}
