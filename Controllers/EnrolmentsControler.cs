using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[Route("api")]
public class EnrolmentsController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db) => this._db = db;

    private static readonly Expression<Func<Enrolment, EnrolmentResponse>> ToResponse = e => new EnrolmentResponse(
        e.EnrolmentId, e.ParticipantId, e.Participant.FullName, e.EventId, e.Event.EventName, e.Event.EventDate,
        e.CategoryId, e.Category.CategoryName, e.EnrolmentDate, e.Status, e.Result != null);

    /// <summary>Enters the logged-in Participant into an event by selecting a category.</summary>
    /// <remarks>
    /// Records the link between the participant, the event and the chosen category.
    /// Returns 409 if already enrolled in this category and 400 if the category is full.
    /// </remarks>
    [HttpPost("categories/{categoryId:int}/enrolments")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentResponse>> Enrol(int categoryId)
    {
        var category = await this._db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category is null)
            return this.Fail(StatusCodes.Status404NotFound, "Category not found", $"No category with id {categoryId}.");

        if (await this._db.Enrolments.AnyAsync(e => e.ParticipantId == this.CurrentUserId && e.CategoryId == categoryId))
            return this.Fail(StatusCodes.Status409Conflict, "Already enrolled", "You are already enrolled in this category.");

        var taken = await this._db.Enrolments.CountAsync(e => e.CategoryId == categoryId);
        if (taken >= category.MaxParticipants)
            return this.Fail(StatusCodes.Status400BadRequest, "Category full", "This category has no spots left.");

        var enrolment = new Enrolment
        {
            ParticipantId = this.CurrentUserId,
            EventId = category.EventId,
            CategoryId = categoryId,
            Status = "Confirmed"
        };
        this._db.Enrolments.Add(enrolment);
        await this._db.SaveChangesAsync();

        var response = await this._db.Enrolments.AsNoTracking().Where(e => e.EnrolmentId == enrolment.EnrolmentId).Select(ToResponse).FirstAsync();
        return this.Created("/api/enrolments/me", response);
    }

    /// <summary>Lists the logged-in Participant's own enrolments.</summary>
    [HttpGet("enrolments/me")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetMine()
    {
        var mine = await this._db.Enrolments.AsNoTracking()
            .Where(e => e.ParticipantId == this.CurrentUserId)
            .OrderBy(e => e.Event.EventDate)
            .Select(ToResponse)
            .ToListAsync();
        return this.Ok(mine);
    }

    /// <summary>Lists everyone enrolled in an event the Organiser owns.</summary>
    [HttpGet("events/{eventId:int}/enrolments")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetForEvent(int eventId)
    {
        var ev = await this._db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null)
            return this.Fail(StatusCodes.Status404NotFound, "Event not found", $"No event with id {eventId}.");
        if (ev.OrganiserId != this.CurrentUserId)
            return this.Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only view enrolments for events you created.");

        var enrolments = await this._db.Enrolments.AsNoTracking()
            .Where(e => e.EventId == eventId)
            .OrderBy(e => e.Category.DistanceKm).ThenBy(e => e.Participant.FullName)
            .Select(ToResponse)
            .ToListAsync();
        return this.Ok(enrolments);
    }

    /// <summary>Cancels the logged-in Participant's own enrolment.</summary>
    /// <remarks>An enrolment that already has a result cannot be cancelled (409).</remarks>
    [HttpDelete("enrolments/{id:int}")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int id)
    {
        var enrolment = await this._db.Enrolments.FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null)
            return this.Fail(StatusCodes.Status404NotFound, "Enrolment not found", $"No enrolment with id {id}.");
        if (enrolment.ParticipantId != this.CurrentUserId)
            return this.Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only cancel your own enrolments.");

        if (await this._db.Results.AnyAsync(r => r.EnrolmentId == id))
            return this.Fail(StatusCodes.Status409Conflict, "Result already captured", "This enrolment already has a result and cannot be cancelled.");

        this._db.Enrolments.Remove(enrolment);
        await this._db.SaveChangesAsync();
        return this.NoContent();
    }
}
