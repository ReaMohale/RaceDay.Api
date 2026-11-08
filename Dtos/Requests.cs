using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class RegisterRequest
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>Either "Organiser" or "Participant".</summary>
    [Required, RegularExpression("^(Organiser|Participant)$", ErrorMessage = "AccountType must be Organiser or Participant.")]
    public string AccountType { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? PhoneNumber { get; set; }
}

public class EventRequest : IValidatableObject
{
    [Required, StringLength(150)]
    public string EventName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    public DateOnly EventDate { get; set; }

    [Required, StringLength(150)]
    public string Location { get; set; } = string.Empty;

    /// <summary>Run, Walk or Cycle.</summary>
    [Required, RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "EventType must be Run, Walk or Cycle.")]
    public string EventType { get; set; } = string.Empty;

    [Range(0.1, 999.99)]
    public decimal DistanceKm { get; set; }

    /// <summary>A missing date would silently become 0001-01-01, so it is checked here.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (this.EventDate == default)
            yield return new ValidationResult("EventDate is required (format yyyy-MM-dd).", new[] { nameof(this.EventDate) });
    }
}

public class CategoryRequest
{
    [Required, StringLength(50)]
    public string CategoryName { get; set; } = string.Empty;

    [Range(0.1, 999.99)]
    public decimal DistanceKm { get; set; }

    [Range(0, 99999)]
    public decimal EntryFee { get; set; }

    [Range(1, 100000)]
    public int MaxParticipants { get; set; }
}

public class ResultRequest
{
    /// <summary>Finish time as hh:mm:ss, e.g. 01:12:07.</summary>
    [Required]
    public TimeSpan FinishTime { get; set; }

    [Range(1, int.MaxValue)]
    public int? Position { get; set; }
}

public class ImageUploadRequest
{
    /// <summary>The image file (JPG, PNG, GIF or WEBP, up to 5 MB).</summary>
    [Required]
    public IFormFile File { get; set; } = null!;
}
