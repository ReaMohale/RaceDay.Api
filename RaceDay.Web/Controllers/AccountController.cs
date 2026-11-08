using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class AccountController : RaceDayController
{
    public AccountController(IRaceDayApi api) : base(api)
    {
    }

    // ------------------------------------------------------------- register

    [HttpGet]
    public IActionResult Register()
    {
        if (this.CurrentUser is not null)
            return this.RedirectToRoleHome();
        return this.View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!this.ModelState.IsValid)
            return this.View(model);

        var registered = await this.Api.RegisterAsync(model);
        if (!registered.Success)
        {
            this.ApplyApiErrors(registered, model);
            return this.View(model);
        }

        // Log the new user straight in so they do not have to type their details twice.
        var login = await this.Api.LoginAsync(model.Email, model.Password);
        if (!login.Success || login.Data is null)
        {
            this.FlashSuccess("Your account was created. Please log in.");
            return this.RedirectToAction(nameof(Login));
        }

        this.HttpContext.Session.SignIn(login.Data.Profile, login.Data.SessionCookie);
        this.FlashSuccess($"Welcome to RaceDay, {login.Data.Profile.FullName}!");
        return this.RedirectToRoleHome();
    }

    // ------------------------------------------------------------- login / logout

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (this.CurrentUser is not null)
            return this.RedirectToRoleHome();
        return this.View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!this.ModelState.IsValid)
            return this.View(model);

        var login = await this.Api.LoginAsync(model.Email, model.Password);
        if (!login.Success || login.Data is null)
        {
            this.ModelState.AddModelError(string.Empty, login.StatusCode == StatusCodes.Status401Unauthorized
                ? "The email or password is incorrect."
                : login.Message);
            model.Password = string.Empty;
            return this.View(model);
        }

        this.HttpContext.Session.SignIn(login.Data.Profile, login.Data.SessionCookie);
        this.FlashSuccess($"Welcome back, {login.Data.Profile.FullName}!");

        if (!string.IsNullOrEmpty(model.ReturnUrl) && this.Url.IsLocalUrl(model.ReturnUrl))
            return this.LocalRedirect(model.ReturnUrl);

        return this.RedirectToRoleHome();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireLogin]
    public async Task<IActionResult> Logout()
    {
        await this.Api.LogoutAsync();           // end the API session too (a failure here does not matter)
        this.HttpContext.Session.Clear();       // forget the user in the MVC session
        this.FlashSuccess("You have been logged out.");
        return this.RedirectToAction(nameof(Login));
    }

    public IActionResult AccessDenied()
    {
        this.Response.StatusCode = StatusCodes.Status403Forbidden;
        return this.View();
    }

    // ------------------------------------------------------------- profile

    [HttpGet]
    [RequireLogin]
    public async Task<IActionResult> Profile()
    {
        var result = await this.Api.GetMeAsync();
        var expired = this.RedirectIfSessionExpired(result);
        if (expired is not null)
            return expired;

        if (!result.Success || result.Data is null)
        {
            this.FlashError(result.Message);
            return this.RedirectToRoleHome();
        }

        return this.View(ToViewModel(result.Data));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireLogin]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!this.ModelState.IsValid)
            return await this.ReloadProfileViewAsync(model);

        var result = await this.Api.UpdateMeAsync(model.FullName, model.PhoneNumber);
        var expired = this.RedirectIfSessionExpired(result);
        if (expired is not null)
            return expired;

        if (!result.Success || result.Data is null)
        {
            this.ApplyApiErrors(result, model);
            return await this.ReloadProfileViewAsync(model);
        }

        // Keep the name shown in the navigation bar up to date.
        this.HttpContext.Session.SetString(SessionKeys.FullName, result.Data.FullName);
        this.FlashSuccess("Your details were updated.");
        return this.RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireLogin]
    public async Task<IActionResult> ProfilePicture(IFormFile? picture)
    {
        if (picture is null || picture.Length == 0)
        {
            this.FlashError("Choose an image to upload.");
            return this.RedirectToAction(nameof(Profile));
        }

        var result = await this.Api.UploadProfilePictureAsync(picture);
        var expired = this.RedirectIfSessionExpired(result);
        if (expired is not null)
            return expired;

        if (result.Success)
            this.FlashSuccess("Your profile picture was updated.");
        else
            this.FlashError(result.Message);

        return this.RedirectToAction(nameof(Profile));
    }

    private async Task<IActionResult> ReloadProfileViewAsync(ProfileViewModel posted)
    {
        // Re-read the read-only parts (email, role, picture) so the page is complete when it is shown again.
        var current = await this.Api.GetMeAsync();
        if (current.Success && current.Data is not null)
        {
            posted.Email = current.Data.Email;
            posted.Role = current.Data.Role;
            posted.ProfilePictureUrl = current.Data.ProfilePictureUrl;
        }
        return this.View(posted);
    }

    private static ProfileViewModel ToViewModel(UserProfile profile) => new()
    {
        FullName = profile.FullName,
        PhoneNumber = profile.PhoneNumber,
        Email = profile.Email,
        Role = profile.Role,
        ProfilePictureUrl = profile.ProfilePictureUrl
    };
}
