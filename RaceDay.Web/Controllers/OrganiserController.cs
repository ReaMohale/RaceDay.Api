using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Organiser pages: dashboard and event management (create, edit, delete, banner image).</summary>
[RequireLogin(Roles.Organiser)]
public class OrganiserController : RaceDayController
{
    public OrganiserController(IRaceDayApi api) : base(api)
    {
    }

    public async Task<IActionResult> Dashboard()
    {
        var result = await this.Api.GetMyEventsAsync();
        var expired = this.RedirectIfSessionExpired(result);
        if (expired is not null)
            return expired;

        if (!result.Success)
            this.ViewData["LoadError"] = result.Message;

        return this.View(new DashboardViewModel { Events = result.Data ?? new List<OrganiserEventSummary>() });
    }

    public async Task<IActionResult> Events()
    {
        var result = await this.Api.GetMyEventsAsync();
        var expired = this.RedirectIfSessionExpired(result);
        if (expired is not null)
            return expired;

        if (!result.Success)
            this.ViewData["LoadError"] = result.Message;

        return this.View(result.Data ?? new List<OrganiserEventSummary>());
    }

    // ------------------------------------------------------------- create

    [HttpGet]
    public IActionResult Create()
    {
        return this.View("EventForm", new EventFormViewModel
        {
            EventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            EventType = "Run",
            DistanceKm = 10
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventFormViewModel model)
    {
        this.ValidateForm(model);
        if (!this.ModelState.IsValid)
            return this.View("EventForm", model);

        var created = await this.Api.CreateEventAsync(model);
        var expired = this.RedirectIfSessionExpired(created);
        if (expired is not null)
            return expired;

        if (!created.Success || created.Data is null)
        {
            this.ApplyApiErrors(created, model);
            return this.View("EventForm", model);
        }

        if (model.Banner is not null && model.Banner.Length > 0)
        {
            var banner = await this.Api.UploadBannerAsync(created.Data.EventId, model.Banner);
            if (!banner.Success)
            {
                this.FlashError($"The event was created, but the banner could not be uploaded: {banner.Message}");
                return this.RedirectToAction(nameof(Events));
            }
        }

        this.FlashSuccess($"{created.Data.EventName} was created. Next, add its categories.");
        return this.RedirectToAction("Index", "Categories", new { eventId = created.Data.EventId });
    }

    // ------------------------------------------------------------- edit

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await this.Api.GetEventAsync(id);
        if (result.StatusCode == StatusCodes.Status404NotFound)
            return this.NotFound();

        var expired = this.RedirectIfSessionExpired(result);
        if (expired is not null)
            return expired;

        if (!result.Success || result.Data is null)
        {
            this.FlashError(result.Message);
            return this.RedirectToAction(nameof(Events));
        }

        if (result.Data.OrganiserId != this.CurrentUser!.UserId)
            return this.RedirectToAction("AccessDenied", "Account");

        var ev = result.Data;
        return this.View("EventForm", new EventFormViewModel
        {
            EventId = ev.EventId,
            EventName = ev.EventName,
            Description = ev.Description,
            EventDate = ev.EventDate,
            Location = ev.Location,
            EventType = ev.EventType,
            DistanceKm = ev.DistanceKm,
            ExistingBannerUrl = ev.BannerUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EventFormViewModel model)
    {
        model.EventId = id;
        this.ValidateForm(model);
        if (!this.ModelState.IsValid)
            return this.View("EventForm", model);

        var updated = await this.Api.UpdateEventAsync(id, model);
        var expired = this.RedirectIfSessionExpired(updated);
        if (expired is not null)
            return expired;

        if (!updated.Success)
        {
            this.ApplyApiErrors(updated, model);
            return this.View("EventForm", model);
        }

        if (model.Banner is not null && model.Banner.Length > 0)
        {
            var banner = await this.Api.UploadBannerAsync(id, model.Banner);
            if (!banner.Success)
            {
                this.FlashError($"The event was saved, but the banner could not be uploaded: {banner.Message}");
                return this.RedirectToAction(nameof(Events));
            }
        }

        this.FlashSuccess("The event was updated.");
        return this.RedirectToAction(nameof(Events));
    }

    // ------------------------------------------------------------- delete

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await this.Api.DeleteEventAsync(id);
        var expired = this.RedirectIfSessionExpired(result);
        if (expired is not null)
            return expired;

        if (result.Success)
            this.FlashSuccess("The event was deleted.");
        else
            this.FlashError(result.Message);

        return this.RedirectToAction(nameof(Events));
    }

    /// <summary>Extra checks the data annotations cannot express.</summary>
    private void ValidateForm(EventFormViewModel model)
    {
        if (model.EventDate == default)
            this.ModelState.AddModelError(nameof(model.EventDate), "Choose the event date.");

        if (model.Banner is not null && model.Banner.Length > 5 * 1024 * 1024)
            this.ModelState.AddModelError(nameof(model.Banner), "The banner image must be 5 MB or smaller.");
    }
}
