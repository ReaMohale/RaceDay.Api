using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Enter your full name.")]
    [StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(20)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Choose a password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "The password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose how you will use RaceDay.")]
    [RegularExpression("^(Organiser|Participant)$", ErrorMessage = "Choose Organiser or Participant.")]
    [Display(Name = "I am a")]
    public string AccountType { get; set; } = Roles.Participant;
}

public class ProfileViewModel
{
    [Required(ErrorMessage = "Enter your full name.")]
    [StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(20)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    // Read-only details shown on the page (not posted back).
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? ProfilePictureUrl { get; set; }
}

public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Enter the event name.")]
    [StringLength(150)]
    [Display(Name = "Event name")]
    public string EventName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "The description can be at most 500 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Choose the event date.")]
    [DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateOnly EventDate { get; set; }

    [Required(ErrorMessage = "Enter the location.")]
    [StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose the event type.")]
    [RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "Choose Run, Walk or Cycle.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = string.Empty;

    [Range(0.1, 999.99, ErrorMessage = "Distance must be between 0.1 and 999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal DistanceKm { get; set; }

    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }

    public string? ExistingBannerUrl { get; set; }
}

public class CategoryFormViewModel
{
    public int EventId { get; set; }
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Enter the category name, e.g. Under 20, Senior or 10km.")]
    [StringLength(50)]
    [Display(Name = "Category name")]
    public string CategoryName { get; set; } = string.Empty;

    [Range(0.1, 999.99, ErrorMessage = "Distance must be between 0.1 and 999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal DistanceKm { get; set; }

    [Range(0, 99999, ErrorMessage = "Entry fee must be between 0 and 99999.")]
    [Display(Name = "Entry fee (R)")]
    public decimal EntryFee { get; set; }

    [Range(1, 100000, ErrorMessage = "Participant limit must be at least 1.")]
    [Display(Name = "Max participants")]
    public int MaxParticipants { get; set; } = 100;
}

// ---- page models ----

public class HomeViewModel
{
    public List<EventItem> Events { get; set; } = new();
    public string? Type { get; set; }
    public string? Search { get; set; }
    public bool UpcomingOnly { get; set; } = true;
}

public class EventDetailsViewModel
{
    public EventItem Event { get; set; } = null!;
    public List<CategoryItem> Categories { get; set; } = new();
    public HashSet<int> EnrolledCategoryIds { get; set; } = new();
}

public class DashboardViewModel
{
    public List<OrganiserEventSummary> Events { get; set; } = new();
    public int TotalEnrolments => this.Events.Sum(e => e.EnrolmentCount);
    public int UpcomingCount => this.Events.Count(e => e.EventDate >= DateOnly.FromDateTime(DateTime.Today));
    public OrganiserEventSummary? NextEvent =>
        this.Events.Where(e => e.EventDate >= DateOnly.FromDateTime(DateTime.Today)).OrderBy(e => e.EventDate).FirstOrDefault();
}

public class CategoriesViewModel
{
    public EventItem Event { get; set; } = null!;
    public List<CategoryItem> Categories { get; set; } = new();
    public CategoryFormViewModel NewCategory { get; set; } = new();
}

public class EventEnrolmentsViewModel
{
    public EventItem Event { get; set; } = null!;
    public List<EnrolmentItem> Enrolments { get; set; } = new();
}

public class ResultsEntryViewModel
{
    public EventItem Event { get; set; } = null!;
    public List<EnrolmentItem> Enrolments { get; set; } = new();
    public List<ResultItem> Results { get; set; } = new();
}
