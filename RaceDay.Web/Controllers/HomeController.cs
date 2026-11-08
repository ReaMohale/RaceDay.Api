using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : RaceDayController
{
    public HomeController(IRaceDayApi api) : base(api)
    {
    }

    /// <summary>Home page: upcoming events with filtering (public).</summary>
    public async Task<IActionResult> Index(string? type, string? search, bool upcomingOnly = true)
    {
        var result = await this.Api.GetEventsAsync(type, search, upcomingOnly);

        var model = new HomeViewModel
        {
            Events = result.Data ?? new List<EventItem>(),
            Type = type,
            Search = search,
            UpcomingOnly = upcomingOnly
        };

        if (!result.Success)
            this.ViewData["LoadError"] = result.Message;

        return this.View(model);
    }

    public IActionResult Error() => this.View();

    /// <summary>Friendly page for 404 and other empty error responses.</summary>
    public IActionResult StatusPage(int code)
    {
        if (code < 400 || code > 599)
            code = StatusCodes.Status404NotFound;

        this.ViewData["Code"] = code;
        this.Response.StatusCode = code;
        return this.View();
    }
}
