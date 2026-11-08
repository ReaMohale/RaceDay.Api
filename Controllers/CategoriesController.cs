using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[Route("api")]
public class CategoriesController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db) => this._db = db;

    private static readonly Expression<Func<Category, CategoryResponse>> ToResponse = c => new CategoryResponse(
        c.CategoryId, c.EventId, c.CategoryName, c.DistanceKm, c.EntryFee, c.MaxParticipants, c.Enrolments.Count());

    /// <summary>Lists the categories of an event with how many spots are taken (public - no login needed).</summary>
    [HttpGet("events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetForEvent(int eventId)
    {
        if (!await this._db.Events.AnyAsync(e => e.EventId == eventId))
            return this.Fail(StatusCodes.Status404NotFound, "Event not found", $"No event with id {eventId}.");

        var categories = await this._db.Categories.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.DistanceKm).ThenBy(c => c.CategoryName)
            .Select(ToResponse)
            .ToListAsync();
        return this.Ok(categories);
    }

    /// <summary>Returns one category (public - no login needed).</summary>
    [HttpGet("categories/{id:int}")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> GetById(int id)
    {
        var category = await this._db.Categories.AsNoTracking().Where(c => c.CategoryId == id).Select(ToResponse).FirstOrDefaultAsync();
        if (category is null)
            return this.Fail(StatusCodes.Status404NotFound, "Category not found", $"No category with id {id}.");
        return this.Ok(category);
    }

    /// <summary>Adds an age or distance category to an event the logged-in Organiser owns.</summary>
    [HttpPost("events/{eventId:int}/categories")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> Create(int eventId, [FromBody] CategoryRequest request)
    {
        var ev = await this._db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null)
            return this.Fail(StatusCodes.Status404NotFound, "Event not found", $"No event with id {eventId}.");
        if (ev.OrganiserId != this.CurrentUserId)
            return this.Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only add categories to events you created.");

        var category = new Category
        {
            EventId = eventId,
            CategoryName = request.CategoryName.Trim(),
            DistanceKm = request.DistanceKm,
            EntryFee = request.EntryFee,
            MaxParticipants = request.MaxParticipants
        };
        this._db.Categories.Add(category);
        await this._db.SaveChangesAsync();

        var response = await this._db.Categories.AsNoTracking().Where(c => c.CategoryId == category.CategoryId).Select(ToResponse).FirstAsync();
        return this.CreatedAtAction(nameof(GetById), new { id = category.CategoryId }, response);
    }

    /// <summary>Updates a category of an event the logged-in Organiser owns.</summary>
    /// <remarks>The participant limit cannot be set below the number of spots already taken (400).</remarks>
    [HttpPut("categories/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> Update(int id, [FromBody] CategoryRequest request)
    {
        var category = await this._db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null)
            return this.Fail(StatusCodes.Status404NotFound, "Category not found", $"No category with id {id}.");
        if (category.Event.OrganiserId != this.CurrentUserId)
            return this.Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only change categories of events you created.");

        var taken = await this._db.Enrolments.CountAsync(e => e.CategoryId == id);
        if (request.MaxParticipants < taken)
            return this.Fail(StatusCodes.Status400BadRequest, "Limit too low",
                $"{taken} participant(s) have already entered this category, so the limit cannot be below {taken}.");

        category.CategoryName = request.CategoryName.Trim();
        category.DistanceKm = request.DistanceKm;
        category.EntryFee = request.EntryFee;
        category.MaxParticipants = request.MaxParticipants;
        await this._db.SaveChangesAsync();

        var response = await this._db.Categories.AsNoTracking().Where(c => c.CategoryId == id).Select(ToResponse).FirstAsync();
        return this.Ok(response);
    }

    /// <summary>Deletes a category of an event the logged-in Organiser owns.</summary>
    /// <remarks>A category that already has enrolments cannot be deleted (409).</remarks>
    [HttpDelete("categories/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await this._db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null)
            return this.Fail(StatusCodes.Status404NotFound, "Category not found", $"No category with id {id}.");
        if (category.Event.OrganiserId != this.CurrentUserId)
            return this.Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only delete categories of events you created.");

        if (await this._db.Enrolments.AnyAsync(e => e.CategoryId == id))
            return this.Fail(StatusCodes.Status409Conflict, "Category has enrolments", "Participants have already entered this category, so it cannot be deleted.");

        this._db.Categories.Remove(category);
        await this._db.SaveChangesAsync();
        return this.NoContent();
    }
}
