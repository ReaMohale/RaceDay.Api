using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Organiser page listing everyone enrolled in one of their events.</summary>
[RequireLogin(Roles.Organiser)]
public class EventEnrolmentsController : RaceDayController
{
    public EventEnrolmentsController(IRaceDayApi api) : base(api)
    {
    }

    public async Task<IActionResult> Index(int eventId)
    {
        var eventResult = await this.Api.GetEventAsync(eventId);
        if (eventResult.StatusCode == StatusCodes.Status404NotFound)
            return this.NotFound();

        var expired = this.RedirectIfSessionExpired(eventResult);
        if (expired is not null)
            return expired;

        if (!eventResult.Success || eventResult.Data is null)
        {
            this.FlashError(eventResult.Message);
            return this.RedirectToAction("Events", "Organiser");
        }

        if (eventResult.Data.OrganiserId != this.CurrentUser!.UserId)
            return this.RedirectToAction("AccessDenied", "Account");

        var enrolments = await this.Api.GetEventEnrolmentsAsync(eventId);
        if (!enrolments.Success)
            this.ViewData["LoadError"] = enrolments.Message;

        return this.View(new EventEnrolmentsViewModel
        {
            Event = eventResult.Data,
            Enrolments = enrolments.Data ?? new List<EnrolmentItem>()
        });
    }
}
