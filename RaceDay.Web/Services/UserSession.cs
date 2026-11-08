using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

public static class SessionKeys
{
    public const string ApiCookie = "ApiCookie";
    public const string UserId = "UserId";
    public const string Role = "Role";
    public const string FullName = "FullName";
}

/// <summary>The signed-in user, as remembered in the MVC session.</summary>
public record SessionUser(int UserId, string Role, string FullName)
{
    public bool IsOrganiser => this.Role == Roles.Organiser;
    public bool IsParticipant => this.Role == Roles.Participant;
}

public static class UserSessionExtensions
{
    public static SessionUser? GetUser(this ISession session)
    {
        var id = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);
        if (id is null || role is null)
            return null;

        return new SessionUser(id.Value, role, session.GetString(SessionKeys.FullName) ?? string.Empty);
    }

    /// <summary>Stores the user and the API session cookie. Any earlier session data is discarded first.</summary>
    public static void SignIn(this ISession session, UserProfile profile, string apiCookie)
    {
        session.Clear();
        session.SetString(SessionKeys.ApiCookie, apiCookie);
        session.SetInt32(SessionKeys.UserId, profile.UserId);
        session.SetString(SessionKeys.Role, profile.Role);
        session.SetString(SessionKeys.FullName, profile.FullName);
    }
}
