using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace expense_tracker_backend.Infrastructure.Services;

public class CurrentUserService
{
    private readonly ClaimsPrincipal? _user;

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        _user = accessor.HttpContext?.User;
    }

    public bool IsAuthenticated =>
        _user?.Identity?.IsAuthenticated == true;

    public string? UserId =>
        _user?.FindFirst("sub")?.Value;

    public string? Email =>
        _user?.FindFirst("email")?.Value;

    public string? UserName =>
        _user?.FindFirst("username")?.Value;
}
