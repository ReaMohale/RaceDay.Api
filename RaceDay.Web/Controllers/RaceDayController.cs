using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Shared behaviour for every controller: API access, the signed-in user, and friendly messages.</summary>
public abstract class RaceDayController : Controller
{
    protected RaceDayController(IRaceDayApi api)
    {
        this.Api = api;
    }

    protected IRaceDayApi Api { get; }

    protected SessionUser? CurrentUser => this.HttpContext.Session.GetUser();

    protected void FlashSuccess(string message) => this.TempData["Success"] = message;

    protected void FlashError(string message) => this.TempData["Error"] = message;

    /// <summary>
    /// If the API says the user is no longer logged in (401), clears the MVC session and returns a redirect to
    /// the login page. Returns null when the session is fine.
    /// </summary>
    protected IActionResult? RedirectIfSessionExpired<T>(ApiResult<T> result)
    {
        if (result.StatusCode != StatusCodes.Status401Unauthorized)
            return null;

        this.HttpContext.Session.Clear();
        this.FlashError("Your session has expired. Please log in again.");
        return this.RedirectToAction("Login", "Account");
    }

    /// <summary>Copies API validation messages onto the form (field-level where the field exists, otherwise at the top).</summary>
    protected void ApplyApiErrors<T>(ApiResult<T> result, object model)
    {
        if (result.Errors.Count == 0)
        {
            this.ModelState.AddModelError(string.Empty, result.Message);
            return;
        }

        foreach (var pair in result.Errors)
        {
            var isKnownField = model.GetType().GetProperty(pair.Key) is not null;
            foreach (var message in pair.Value)
                this.ModelState.AddModelError(isKnownField ? pair.Key : string.Empty, message);
        }
    }

    /// <summary>The page for the signed-in user's role: organisers land on the dashboard, everyone else on the home page.</summary>
    protected IActionResult RedirectToRoleHome()
    {
        return this.CurrentUser?.IsOrganiser == true
            ? this.RedirectToAction("Dashboard", "Organiser")
            : this.RedirectToAction("Index", "Home");
    }
}
