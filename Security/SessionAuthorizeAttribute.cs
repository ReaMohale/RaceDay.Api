using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Security;

/// <summary>
/// Protects an endpoint with the server-side session.
/// <c>[SessionAuthorize]</c> needs any logged-in user (401 otherwise);
/// <c>[SessionAuthorize(Roles.Organiser)]</c> also needs that role (403 otherwise).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SessionAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string? _role;

    public SessionAuthorizeAttribute(string? role = null)
    {
        this._role = role;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        await session.LoadAsync(context.HttpContext.RequestAborted);

        var userId = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);

        if (userId is null || string.IsNullOrEmpty(role))
        {
            context.Result = Problem(StatusCodes.Status401Unauthorized, "Unauthorized",
                "You need to log in first (POST /api/auth/login).");
            return;
        }

        if (this._role is not null && !string.Equals(role, this._role, StringComparison.Ordinal))
        {
            context.Result = Problem(StatusCodes.Status403Forbidden, "Forbidden",
                $"This action is only available to {this._role}s.");
        }
    }

    private static ObjectResult Problem(int status, string title, string detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };
}
