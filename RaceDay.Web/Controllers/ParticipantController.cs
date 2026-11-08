using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Pages only participants can see: their enrolments and their race history.</summary>
[RequireLogin(Roles.Participant)]
public class ParticipantController : RaceDayController
{
    public ParticipantController(IRaceDayApi api) : base(api)
    {
    }

    public async Task<IActionResult> MyEnrolments()
    {
        var result = await this.Api.GetMyEnrolmentsAsync();
        var expired = this.RedirectIfSessionExpired(result);
        if (expired is not null)
            return expired;

        if (!result.Success)
            this.ViewData["LoadError"] = result.Message;

        return this.View(result.Data ?? new List<EnrolmentItem>());
    }

    public async Task<IActionResult> MyResults()
    {
        var result = await this.Api.GetMyResultsAsync();
        var expired = this.RedirectIfSessionExpired(result);
        if (expired is not null)
            return expired;

        if (!result.Success)
            this.ViewData["LoadError"] = result.Message;

        return this.View(result.Data ?? new List<ResultItem>());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int enrolmentId)
    {
        var result = await this.Api.CancelEnrolmentAsync(enrolmentId);
        var expired = this.RedirectIfSessionExpired(result);
        if (expired is not null)
            return expired;

        if (result.Success)
            this.FlashSuccess("Your enrolment was cancelled.");
        else
            this.FlashError(result.Message);

        return this.RedirectToAction(nameof(MyEnrolments));
    }
}
