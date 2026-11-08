using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Shared behaviour for every API controller: the logged-in user and consistent error responses.</summary>
[ApiController]
public abstract class RaceDayControllerBase : ControllerBase
{
    /// <summary>The id of the logged-in user (OrganiserId or ParticipantId). Only meaningful behind [SessionAuthorize].</summary>
    protected int CurrentUserId => this.HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    /// <summary>"Organiser" or "Participant". Only meaningful behind [SessionAuthorize].</summary>
    protected string CurrentRole => this.HttpContext.Session.GetString(SessionKeys.Role) ?? string.Empty;

    /// <summary>A problem-details error response with the given status.</summary>
    protected ObjectResult Fail(int status, string title, string detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };
}
