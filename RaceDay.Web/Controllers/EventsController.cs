using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class EventsController : RaceDayController
{
    public EventsController(IRaceDayApi api) : base(api)
    {
    }

    /// <summary>Full event information and its categories (public). Logged-in participants can enter from here.</summary>
    public async Task<IActionResult> Details(int id)
    {
        var eventResult = await this.Api.GetEventAsync(id);
        if (eventResult.StatusCode == StatusCodes.Status404NotFound)
            return this.NotFound();

        if (!eventResult.Success || eventResult.Data is null)
        {
            this.FlashError(eventResult.Message);
            return this.RedirectToAction("Index", "Home");
        }

        var categoriesResult = await this.Api.GetCategoriesAsync(id);
        var model = new EventDetailsViewModel
        {
            Event = eventResult.Data,
            Categories = categoriesResult.Data ?? new List<CategoryItem>()
        };

        if (!categoriesResult.Success)
            this.ViewData["LoadError"] = categoriesResult.Message;

        // For a logged-in participant, remember which categories they have already entered.
        if (this.CurrentUser?.IsParticipant == true)
        {
            var mine = await this.Api.GetMyEnrolmentsAsync();
            if (mine.Success && mine.Data is not null)
                model.EnrolledCategoryIds = mine.Data.Select(e => e.CategoryId).ToHashSet();
        }

        return this.View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireLogin(Roles.Participant)]
    public async Task<IActionResult> Enrol(int eventId, int categoryId)
    {
        var result = await this.Api.EnrolAsync(categoryId);
        var expired = this.RedirectIfSessionExpired(result);
        if (expired is not null)
            return expired;

        if (!result.Success)
        {
            this.FlashError(result.Message);
            return this.RedirectToAction(nameof(Details), new { id = eventId });
        }

        this.FlashSuccess($"You are enrolled in {result.Data?.CategoryName ?? "the event"}. Good luck!");
        return this.RedirectToAction("MyEnrolments", "Participant");
    }
}
