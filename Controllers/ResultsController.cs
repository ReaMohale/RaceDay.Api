using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[Route("api")]
public class ResultsController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db) => this._db = db;

    private static readonly Expression<Func<Result, ResultResponse>> ToResponse = r => new ResultResponse(
        r.ResultId, r.EnrolmentId, r.Enrolment.ParticipantId, r.Enrolment.Participant.FullName,
        r.Enrolment.EventId, r.Enrolment.Event.EventName, r.Enrolment.Event.EventDate,
        r.Enrolment.Category.CategoryName, r.FinishTime, r.Position, r.RecordedAt,
        r.Enrolment.Category.Enrolments.Count(x => x.Result != null));

    /// <summary>Captures the finish time and position for an enrolment in an event the Organiser owns.</summary>
    /// <remarks>finishTime format: hh:mm:ss (must be under 24 hours). One result per enrolment (409 if it already exists).</remarks>
    [HttpPost("enrolments/{enrolmentId:int}/results")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> Capture(int enrolmentId, [FromBody] ResultRequest request)
    {
        var enrolment = await this._db.Enrolments.AsNoTracking().Include(e => e.Event).FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);
        if (enrolment is null)
            return this.Fail(StatusCodes.Status404NotFound, "Enrolment not found", $"No enrolment with id {enrolmentId}.");
        if (enrolment.Event.OrganiserId != this.CurrentUserId)
            return this.Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only capture results for events you created.");

        if (request.FinishTime <= TimeSpan.Zero || request.FinishTime >= TimeSpan.FromHours(24))
            return this.Fail(StatusCodes.Status400BadRequest, "Invalid finish time", "Finish time must be greater than 00:00:00 and less than 24 hours.");

        if (await this._db.Results.AnyAsync(r => r.EnrolmentId == enrolmentId))
            return this.Fail(StatusCodes.Status409Conflict, "Result already captured", "This enrolment already has a result.");

        var result = new Result
        {
            EnrolmentId = enrolmentId,
            FinishTime = request.FinishTime,
            Position = request.Position
        };
        this._db.Results.Add(result);
        await this._db.SaveChangesAsync();

        var response = await this._db.Results.AsNoTracking().Where(r => r.ResultId == result.ResultId).Select(ToResponse).FirstAsync();
        return this.CreatedAtAction(nameof(GetForEnrolment), new { enrolmentId }, response);
    }

    /// <summary>Returns the result for an enrolment - visible to its Participant or the event's Organiser only.</summary>
    [HttpGet("enrolments/{enrolmentId:int}/results")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultResponse>> GetForEnrolment(int enrolmentId)
    {
        var enrolment = await this._db.Enrolments.AsNoTracking().Include(e => e.Event).FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);
        if (enrolment is null)
            return this.Fail(StatusCodes.Status404NotFound, "Enrolment not found", $"No enrolment with id {enrolmentId}.");

        var allowed = this.CurrentRole == Roles.Participant
            ? enrolment.ParticipantId == this.CurrentUserId
            : enrolment.Event.OrganiserId == this.CurrentUserId;
        if (!allowed)
            return this.Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only view results for your own enrolments or events you created.");

        var result = await this._db.Results.AsNoTracking().Where(r => r.EnrolmentId == enrolmentId).Select(ToResponse).FirstOrDefaultAsync();
        if (result is null)
            return this.Fail(StatusCodes.Status404NotFound, "No result yet", "No result has been captured for this enrolment.");
        return this.Ok(result);
    }

    /// <summary>Lists every captured result for an event the Organiser owns.</summary>
    [HttpGet("events/{eventId:int}/results")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetForEvent(int eventId)
    {
        var ev = await this._db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null)
            return this.Fail(StatusCodes.Status404NotFound, "Event not found", $"No event with id {eventId}.");
        if (ev.OrganiserId != this.CurrentUserId)
            return this.Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only view results for events you created.");

        var results = await this._db.Results.AsNoTracking()
            .Where(r => r.Enrolment.EventId == eventId)
            .OrderBy(r => r.Enrolment.Category.DistanceKm).ThenBy(r => r.Position)
            .Select(ToResponse)
            .ToListAsync();
        return this.Ok(results);
    }

    /// <summary>Returns the logged-in Participant's full result history.</summary>
    [HttpGet("participants/me/results")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetMine()
    {
        var results = await this._db.Results.AsNoTracking()
            .Where(r => r.Enrolment.ParticipantId == this.CurrentUserId)
            .OrderByDescending(r => r.Enrolment.Event.EventDate)
            .Select(ToResponse)
            .ToListAsync();
        return this.Ok(results);
    }
}
