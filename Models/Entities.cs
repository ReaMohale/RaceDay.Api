namespace RaceDay.Api.Models;

public class Organiser
{
    public int OrganiserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    /// <summary>Azure Blob URL of the profile picture (null until one is uploaded).</summary>
    public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}

public class Participant
{
    public int ParticipantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    /// <summary>Azure Blob URL of the profile picture (null until one is uploaded).</summary>
    public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    /// <summary>Run, Walk or Cycle.</summary>
    public string EventType { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    /// <summary>Azure Blob URL of the event banner image (null until one is uploaded).</summary>
    public string? BannerUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Organiser Organiser { get; set; } = null!;
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public decimal EntryFee { get; set; }
    public int MaxParticipants { get; set; }

    public Event Event { get; set; } = null!;
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public int EventId { get; set; }
    public int CategoryId { get; set; }
    public DateTime EnrolmentDate { get; set; } = DateTime.Now;
    public string Status { get; set; } = "Confirmed";

    public Participant Participant { get; set; } = null!;
    public Event Event { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Result? Result { get; set; }
}

public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public TimeSpan FinishTime { get; set; }
    public int? Position { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.Now;

    public Enrolment Enrolment { get; set; } = null!;
}
