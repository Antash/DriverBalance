using DriverBalance.Data;

namespace DriverBalance.Web;

/// <summary>The acting user is the `sub` claim of the authenticated principal.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public long UserId
    {
        get
        {
            var sub = accessor.HttpContext?.User.FindFirst("sub")?.Value;
            return long.TryParse(sub, out var id) && id > 0
                ? id
                : throw new InvalidOperationException("No authenticated user for this request.");
        }
    }
}
