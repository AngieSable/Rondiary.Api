using System.Security.Claims;

namespace Rondiary.Api;


public static class UserExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var s = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.Parse(s!);
    }
}