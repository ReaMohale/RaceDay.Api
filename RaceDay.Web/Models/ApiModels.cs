namespace RaceDay.Web.Models;

// Shapes of the JSON returned by the RaceDay API (Part 2). The MVC app only displays this data.

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

public record UserProfile(int UserId, string FullName, string Email, string? PhoneNumber, string Role, DateTime CreatedAt,
    string? ProfilePictureUrl);

public record EventItem(int EventId, int OrganiserId, string OrganiserName, string EventName, string? Description,
    DateOnly EventDate, string Location, string EventType, decimal DistanceKm, string? BannerUrl);

public record CategoryItem(int CategoryId, int EventId, string CategoryName, decimal DistanceKm, decimal EntryFee,
    int MaxParticipants, int SpotsTaken)
{
    public int SpotsLeft => Math.Max(0, this.MaxParticipants - this.SpotsTaken);
    public bool IsFull => this.SpotsTaken >= this.MaxParticipants;
}

public record EnrolmentItem(int EnrolmentId, int ParticipantId, string ParticipantName, int EventId, string EventName,
    DateOnly EventDate, int CategoryId, string CategoryName, DateTime EnrolmentDate, string Status, bool HasResult);

public record ResultItem(int ResultId, int EnrolmentId, int ParticipantId, string ParticipantName, int EventId,
    string EventName, DateOnly EventDate, string CategoryName, TimeSpan FinishTime, int? Position, DateTime RecordedAt,
    int TotalFinishers);

public record OrganiserEventSummary(int EventId, string EventName, DateOnly EventDate, string Location, string EventType,
    decimal DistanceKm, string? BannerUrl, int CategoryCount, int EnrolmentCount, int ResultCount);

/// <summary>What a successful API login gives back: the profile plus the API's session cookie.</summary>
public record LoginData(UserProfile Profile, string SessionCookie);
