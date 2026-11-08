namespace RaceDay.Api.Security;

/// <summary>The two kinds of account in RaceDay.</summary>
public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

/// <summary>Keys for the values the API keeps in its server-side session.</summary>
public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
}
