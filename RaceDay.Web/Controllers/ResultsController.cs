using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Organiser page to capture and publish finish times and positions after an event.</summary>
[RequireLogin(Roles.Organiser)]
public class ResultsController : RaceDayController
{
    public ResultsController(IRaceDayApi api) : base(api)
    {
    }

    public async Task<IActionResult> Entry(int eventId)
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
        var results = await this.Api.GetEventResultsAsync(eventId);
        if (!enrolments.Success)
            this.ViewData["LoadError"] = enrolments.Message;
        else if (!results.Success)
            this.ViewData["LoadError"] = results.Message;

        return this.View(new ResultsEntryViewModel
        {
            Event = eventResult.Data,
            Enrolments = enrolments.Data ?? new List<EnrolmentItem>(),
            Results = results.Data ?? new List<ResultItem>()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Capture(int eventId, int enrolmentId, string? finishTime, int? position)
    {
        // h:mm:ss with hours 0-23. TryParseExact means a typo can never crash the page (e.g. 99:99:99).
        if (!TimeSpan.TryParseExact(finishTime?.Trim(), @"h\:mm\:ss", CultureInfo.InvariantCulture, out var time)
            || time <= TimeSpan.Zero)
        {
            this.FlashError("Enter the finish time as hours:minutes:seconds, for example 0:47:31 (up to 23:59:59).");
            return this.RedirectToAction(nameof(Entry), new { eventId });
        }

        if (position is not null && position < 1)
        {
            this.FlashError("The finishing position must be 1 or higher.");
            return this.RedirectToAction(nameof(Entry), new { eventId });
        }

        var result = await this.Api.CaptureResultAsync(enrolmentId, time, position);
        var expired = this.RedirectIfSessionExpired(result);
        if (expired is not null)
            return expired;

        if (result.Success)
            this.FlashSuccess($"Result published for {result.Data?.ParticipantName ?? "the participant"}.");
        else
            this.FlashError(result.Message);

        return this.RedirectToAction(nameof(Entry), new { eventId });
    }
}
