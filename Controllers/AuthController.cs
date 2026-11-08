using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api/auth")]
public class AuthController : RaceDayControllerBase
{
    // Verified when the e-mail is unknown so a wrong e-mail and a wrong password take the same time.
    private static readonly Lazy<string> DummyHash = new(() => new PasswordService().Hash("not-a-real-password"));

    private readonly RaceDayDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IBlobStorageService _blobs;

    public AuthController(RaceDayDbContext db, IPasswordService passwords, IBlobStorageService blobs)
    {
        this._db = db;
        this._passwords = passwords;
        this._blobs = blobs;
    }

    /// <summary>Creates an Organiser or Participant account.</summary>
    /// <remarks>accountType must be Organiser or Participant. The password is stored as a salted hash. Log in afterwards with POST /api/auth/login.</remarks>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserProfileResponse>> Register([FromBody] RegisterRequest request)
    {
        var email = NormaliseEmail(request.Email);
        if (await this.EmailInUse(email))
            return this.Fail(StatusCodes.Status409Conflict, "Email already registered", "An account with this email address already exists.");

        var hash = this._passwords.Hash(request.Password);
        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        var fullName = request.FullName.Trim();

        UserProfileResponse profile;
        if (request.AccountType == Roles.Organiser)
        {
            var organiser = new Organiser { FullName = fullName, Email = email, PasswordHash = hash, PhoneNumber = phone };
            this._db.Organisers.Add(organiser);
            if (!await this.TrySaveAsync())
                return this.EmailTaken();
            profile = new UserProfileResponse(organiser.OrganiserId, organiser.FullName, organiser.Email,
                organiser.PhoneNumber, Roles.Organiser, organiser.CreatedAt);
        }
        else
        {
            var participant = new Participant { FullName = fullName, Email = email, PasswordHash = hash, PhoneNumber = phone };
            this._db.Participants.Add(participant);
            if (!await this.TrySaveAsync())
                return this.EmailTaken();
            profile = new UserProfileResponse(participant.ParticipantId, participant.FullName, participant.Email,
                participant.PhoneNumber, Roles.Participant, participant.CreatedAt);
        }

        return this.Created("/api/users/me", profile);
    }

    /// <summary>Logs in and starts a server-side session (a session cookie is returned).</summary>
    /// <remarks>Swagger UI sends the cookie automatically on later calls. Returns 401 for a wrong e-mail or password.</remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> Login([FromBody] LoginRequest request)
    {
        var email = NormaliseEmail(request.Email);

        var organiser = await this._db.Organisers.AsNoTracking().FirstOrDefaultAsync(o => o.Email == email);
        var participant = organiser is null
            ? await this._db.Participants.AsNoTracking().FirstOrDefaultAsync(p => p.Email == email)
            : null;

        var storedHash = organiser?.PasswordHash ?? participant?.PasswordHash;
        var passwordOk = this._passwords.Verify(request.Password, storedHash ?? DummyHash.Value);
        if (storedHash is null || !passwordOk)
            return this.Fail(StatusCodes.Status401Unauthorized, "Invalid credentials", "The email or password is incorrect.");

        UserProfileResponse profile = organiser is not null
            ? new UserProfileResponse(organiser.OrganiserId, organiser.FullName, organiser.Email, organiser.PhoneNumber,
                Roles.Organiser, organiser.CreatedAt, this._blobs.GetReadUrl(organiser.ProfilePictureUrl))
            : new UserProfileResponse(participant!.ParticipantId, participant.FullName, participant.Email, participant.PhoneNumber,
                Roles.Participant, participant.CreatedAt, this._blobs.GetReadUrl(participant.ProfilePictureUrl));

        var session = this.HttpContext.Session;
        await session.LoadAsync(this.HttpContext.RequestAborted);
        session.Clear();
        session.SetInt32(SessionKeys.UserId, profile.UserId);
        session.SetString(SessionKeys.Role, profile.Role);

        return this.Ok(profile);
    }

    /// <summary>Ends the session. Safe to call when nobody is logged in.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        var session = this.HttpContext.Session;
        await session.LoadAsync(this.HttpContext.RequestAborted);
        session.Clear();
        return this.NoContent();
    }

    private static string NormaliseEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>One e-mail address belongs to one account across both account types.</summary>
    private async Task<bool> EmailInUse(string email) =>
        await this._db.Organisers.AnyAsync(o => o.Email == email)
        || await this._db.Participants.AnyAsync(p => p.Email == email);

    /// <summary>Saves; false means two sign-ups with the same e-mail raced and the database unique index rejected one.</summary>
    private async Task<bool> TrySaveAsync()
    {
        try
        {
            await this._db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    private ObjectResult EmailTaken() =>
        this.Fail(StatusCodes.Status409Conflict, "Email already registered", "An account with this email address already exists.");
}
