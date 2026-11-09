using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Web.Services;

/// <summary>
/// Protects a controller or action using the web session. Not logged in -> sent to the login page.
/// Logged in with the wrong role -> sent to the "access denied" page.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RoleAuthorizeAttribute : ActionFilterAttribute
{
    private readonly string? _role;

    public RoleAuthorizeAttribute(string? role = null) => _role = role;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.Session.GetUser();

        if (user is null)
        {
            var request = context.HttpContext.Request;
            var returnUrl = request.Method == "GET" ? request.Path + request.QueryString : null;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        if (_role is not null && user.Role != _role)
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Home", null);
        }
    }
}
