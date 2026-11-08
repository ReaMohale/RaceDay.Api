using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Keeps a page private. Not logged in -> sent to the login page (and brought back afterwards).
/// Logged in with the wrong role -> sent to the "access denied" page.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireLoginAttribute : ActionFilterAttribute
{
    private readonly string? _role;

    public RequireLoginAttribute(string? role = null)
    {
        this._role = role;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.Session.GetUser();

        if (user is null)
        {
            var request = context.HttpContext.Request;
            var returnUrl = request.Path.Value + request.QueryString.Value;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        if (this._role is not null && user.Role != this._role)
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
        }
    }
}
