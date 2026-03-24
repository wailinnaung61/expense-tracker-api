using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace expense_tracker_backend.API.Controllers
{
    public abstract class BaseController : ControllerBase
    {
        private const string BearerPrefix = "Bearer ";

        protected Guid? UserId =>
            Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var userId)
            ? userId
            : (Guid?)null;

        protected string GetAccessToken()
        {
            var authHeader = HttpContext.Request.Headers.Authorization.ToString();
            return authHeader.StartsWith(BearerPrefix) 
                ? authHeader[BearerPrefix.Length..] 
                : authHeader;
        }

        protected ActionResult SuccessResponse(string message) => Ok(new { message });

        protected ActionResult ErrorResponse(string message) => BadRequest(new { message });
    }
}
