using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api")]
public class EventsController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IBlobStorageService _blobs;

    public EventsController(RaceDayDbContext db, IBlobStorageService blobs)
    {
        this._db = db;
        this._blobs = blobs;
    }

    private static readonly Expression<Func<Event, EventResponse>> ToResponse = e => new EventResponse(
        e.EventId, e.OrganiserId, e.Organiser.FullName, e.EventName, e.Description,
        e.EventDate, e.Location, e.EventType, e.DistanceKm, e.BannerUrl);

    /// <summary>Swaps the stored banner URL for a short-lived readable (SAS) URL.</summary>
    private EventResponse WithReadUrl(EventResponse response) =>
        response with { BannerUrl = this._blobs.GetReadUrl(response.BannerUrl) };

    /// <summary>Lists events (public - no login needed), optionally filtered.</summary>
    /// <remarks>
    /// Optional query parameters: type (Run, Walk or Cycle), search (matches name or location)
    /// and upcomingOnly=true (hide events that have already taken place).
    /// </remarks>
    [HttpGet("events")]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EventResponse>>> GetAll(
        [FromQuery] string? type, [FromQuery] string? search, [FromQuery] bool upcomingOnly = false)
    {
        var query = this._db.Events.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(e => e.EventType == type);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e => e.EventName.Contains(term) || e.Location.Contains(term));
        }

        if (upcomingOnly)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            query = query.Where(e => e.EventDate >= today);
        }

        var events = await query.OrderBy(e => e.EventDate).Select(ToResponse).ToListAsync();
        return this.Ok(events.Select(this.WithReadUrl).ToList());
    }

    /// <summary>Returns one event's details (public - no login needed).</summary>
    [HttpGet("events/{id:int}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> GetById(int id)
    {
        var ev = await this._db.Events.AsNoTracking().Where(e => e.EventId == id).Select(ToResponse).FirstOrDefaultAsync();
        if (ev is null)
            return this.Fail(StatusCodes.Status404NotFound, "Event not found", $"No event with id {id}.");
        return this.Ok(this.WithReadUrl(ev));
    }

    /// <summary>Lists the logged-in Organiser's own events with category, entry and result counts (dashboard data).</summary>
    [HttpGet("organisers/me/events")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<OrganiserEventSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<OrganiserEventSummary>>> GetMyEvents()
    {
        var summaries = await this._db.Events.AsNoTracking()
            .Where(e => e.OrganiserId == this.CurrentUserId)
            .OrderBy(e => e.EventDate)
            .Select(e => new OrganiserEventSummary(
                e.EventId, e.EventName, e.EventDate, e.Location, e.EventType, e.DistanceKm, e.BannerUrl,
                e.Categories.Count(), e.Enrolments.Count(), e.Enrolments.Count(en => en.Result != null)))
            .ToListAsync();

        return this.Ok(summaries.Select(s => s with { BannerUrl = this._blobs.GetReadUrl(s.BannerUrl) }).ToList());
    }

    /// <summary>Creates an event owned by the logged-in Organiser.</summary>
    /// <remarks>eventType must be Run, Walk or Cycle. eventDate format: yyyy-MM-dd. Upload the banner afterwards with POST /api/events/{id}/banner.</remarks>
    [HttpPost("events")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventResponse>> Create([FromBody] EventRequest request)
    {
        var ev = new Event
        {
            OrganiserId = this.CurrentUserId,
            EventName = request.EventName.Trim(),
            Description = request.Description,
            EventDate = request.EventDate,
            Location = request.Location.Trim(),
            EventType = request.EventType,
            DistanceKm = request.DistanceKm
        };
        this._db.Events.Add(ev);
        await this._db.SaveChangesAsync();

        var response = await this._db.Events.AsNoTracking().Where(e => e.EventId == ev.EventId).Select(ToResponse).FirstAsync();
        return this.CreatedAtAction(nameof(GetById), new { id = ev.EventId }, this.WithReadUrl(response));
    }

    /// <summary>Updates an event the logged-in Organiser owns.</summary>
    [HttpPut("events/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> Update(int id, [FromBody] EventRequest request)
    {
        var ev = await this._db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null)
            return this.Fail(StatusCodes.Status404NotFound, "Event not found", $"No event with id {id}.");
        if (ev.OrganiserId != this.CurrentUserId)
            return this.Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only change events you created.");

        ev.EventName = request.EventName.Trim();
        ev.Description = request.Description;
        ev.EventDate = request.EventDate;
        ev.Location = request.Location.Trim();
        ev.EventType = request.EventType;
        ev.DistanceKm = request.DistanceKm;
        await this._db.SaveChangesAsync();

        var response = await this._db.Events.AsNoTracking().Where(e => e.EventId == id).Select(ToResponse).FirstAsync();
        return this.Ok(this.WithReadUrl(response));
    }

    /// <summary>Uploads (or replaces) the banner image of an event the Organiser owns, storing it in Azure Blob Storage.</summary>
    /// <remarks>Send multipart/form-data with a "File" field. JPG, PNG, GIF or WEBP, up to 5 MB. The blob URL is saved on the event.</remarks>
    [HttpPost("events/{id:int}/banner")]
    [SessionAuthorize(Roles.Organiser)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EventResponse>> UploadBanner(int id, [FromForm] ImageUploadRequest request)
    {
        var ev = await this._db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null)
            return this.Fail(StatusCodes.Status404NotFound, "Event not found", $"No event with id {id}.");
        if (ev.OrganiserId != this.CurrentUserId)
            return this.Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only change events you created.");

        var outcome = await ImageUploadHandler.HandleAsync(request.File, this._blobs, "banners", this.HttpContext.RequestAborted);
        if (!outcome.Succeeded)
            return this.Fail(outcome.Status, outcome.Title, outcome.Detail);

        ev.BannerUrl = outcome.Url;
        await this._db.SaveChangesAsync();

        var response = await this._db.Events.AsNoTracking().Where(e => e.EventId == id).Select(ToResponse).FirstAsync();
        return this.Ok(this.WithReadUrl(response));
    }

    /// <summary>Deletes an event (and its categories) the logged-in Organiser owns.</summary>
    /// <remarks>An event that already has enrolments cannot be deleted (409).</remarks>
    [HttpDelete("events/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await this._db.Events.Include(e => e.Categories).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null)
            return this.Fail(StatusCodes.Status404NotFound, "Event not found", $"No event with id {id}.");
        if (ev.OrganiserId != this.CurrentUserId)
            return this.Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only delete events you created.");

        if (await this._db.Enrolments.AnyAsync(e => e.EventId == id))
            return this.Fail(StatusCodes.Status409Conflict, "Event has enrolments", "This event already has enrolments and cannot be deleted.");

        this._db.Categories.RemoveRange(ev.Categories);
        this._db.Events.Remove(ev);
        await this._db.SaveChangesAsync();
        return this.NoContent();
    }
}
