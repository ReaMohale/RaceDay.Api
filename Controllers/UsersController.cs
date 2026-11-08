using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api")]
public class UsersController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IBlobStorageService _blobs;

    public UsersController(RaceDayDbContext db, IBlobStorageService blobs)
    {
        this._db = db;
        this._blobs = blobs;
    }

    /// <summary>Returns the profile of the logged-in user (Organiser or Participant).</summary>
    [HttpGet("users/me")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> GetMe()
    {
        var profile = await this.LoadProfile();
        if (profile is null)
            return this.Fail(StatusCodes.Status401Unauthorized, "Unauthorized", "Your account no longer exists. Please log in again.");
        return this.Ok(profile);
    }

    /// <summary>Updates the logged-in user's own name and phone number.</summary>
    [HttpPut("users/me")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        if (this.CurrentRole == Roles.Organiser)
        {
            var organiser = await this._db.Organisers.FirstOrDefaultAsync(o => o.OrganiserId == this.CurrentUserId);
            if (organiser is null)
                return this.Fail(StatusCodes.Status401Unauthorized, "Unauthorized", "Your account no longer exists. Please log in again.");
            organiser.FullName = request.FullName.Trim();
            organiser.PhoneNumber = request.PhoneNumber;
        }
        else
        {
            var participant = await this._db.Participants.FirstOrDefaultAsync(p => p.ParticipantId == this.CurrentUserId);
            if (participant is null)
                return this.Fail(StatusCodes.Status401Unauthorized, "Unauthorized", "Your account no longer exists. Please log in again.");
            participant.FullName = request.FullName.Trim();
            participant.PhoneNumber = request.PhoneNumber;
        }

        await this._db.SaveChangesAsync();
        return this.Ok(await this.LoadProfile());
    }

    /// <summary>Uploads (or replaces) the logged-in user's profile picture, storing it in Azure Blob Storage.</summary>
    /// <remarks>Send multipart/form-data with a "File" field. JPG, PNG, GIF or WEBP, up to 5 MB. The blob URL is saved on the user.</remarks>
    [HttpPost("users/me/picture")]
    [SessionAuthorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<UserProfileResponse>> UploadPicture([FromForm] ImageUploadRequest request)
    {
        var outcome = await ImageUploadHandler.HandleAsync(request.File, this._blobs, "profiles", this.HttpContext.RequestAborted);
        if (!outcome.Succeeded)
            return this.Fail(outcome.Status, outcome.Title, outcome.Detail);

        if (this.CurrentRole == Roles.Organiser)
        {
            var organiser = await this._db.Organisers.FirstOrDefaultAsync(o => o.OrganiserId == this.CurrentUserId);
            if (organiser is null)
                return this.Fail(StatusCodes.Status401Unauthorized, "Unauthorized", "Your account no longer exists. Please log in again.");
            organiser.ProfilePictureUrl = outcome.Url;
        }
        else
        {
            var participant = await this._db.Participants.FirstOrDefaultAsync(p => p.ParticipantId == this.CurrentUserId);
            if (participant is null)
                return this.Fail(StatusCodes.Status401Unauthorized, "Unauthorized", "Your account no longer exists. Please log in again.");
            participant.ProfilePictureUrl = outcome.Url;
        }

        await this._db.SaveChangesAsync();
        return this.Ok(await this.LoadProfile());
    }

    /// <summary>Lets an organiser view a participant who is entered in one of their events.</summary>
    [HttpGet("participants/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponse>> GetParticipant(int id)
    {
        var participant = await this._db.Participants.AsNoTracking().FirstOrDefaultAsync(p => p.ParticipantId == id);
        if (participant is null)
            return this.Fail(StatusCodes.Status404NotFound, "Participant not found", $"No participant with id {id}.");

        var inMyEvents = await this._db.Enrolments.AnyAsync(e => e.ParticipantId == id && e.Event.OrganiserId == this.CurrentUserId);
        if (!inMyEvents)
            return this.Fail(StatusCodes.Status403Forbidden, "Forbidden", "This participant is not entered in any of your events.");

        return this.Ok(new UserProfileResponse(participant.ParticipantId, participant.FullName, participant.Email,
            participant.PhoneNumber, Roles.Participant, participant.CreatedAt,
            this._blobs.GetReadUrl(participant.ProfilePictureUrl)));
    }

    private async Task<UserProfileResponse?> LoadProfile()
    {
        var id = this.CurrentUserId;
        if (this.CurrentRole == Roles.Organiser)
        {
            var o = await this._db.Organisers.AsNoTracking().FirstOrDefaultAsync(x => x.OrganiserId == id);
            return o is null ? null
                : new UserProfileResponse(o.OrganiserId, o.FullName, o.Email, o.PhoneNumber, Roles.Organiser, o.CreatedAt,
                    this._blobs.GetReadUrl(o.ProfilePictureUrl));
        }

        var p = await this._db.Participants.AsNoTracking().FirstOrDefaultAsync(x => x.ParticipantId == id);
        return p is null ? null
            : new UserProfileResponse(p.ParticipantId, p.FullName, p.Email, p.PhoneNumber, Roles.Participant, p.CreatedAt,
                this._blobs.GetReadUrl(p.ProfilePictureUrl));
    }
}
