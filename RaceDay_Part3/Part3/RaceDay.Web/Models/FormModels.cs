using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "That doesn't look like a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please enter your full name."), StringLength(100, MinimumLength = 2, ErrorMessage = "Your name must be between 2 and 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "That doesn't look like a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a password."), StringLength(100, MinimumLength = 8, ErrorMessage = "Your password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password."), Compare(nameof(Password), ErrorMessage = "The two passwords do not match.")]
    [DataType(DataType.Password), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a role.")]
    public string Role { get; set; } = AppRoles.Participant;

    [Display(Name = "Organiser registration code")]
    public string? OrganiserCode { get; set; }
}

public class ProfileViewModel
{
    [Required(ErrorMessage = "Please enter your full name."), StringLength(100, MinimumLength = 2, ErrorMessage = "Your name must be between 2 and 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "That doesn't look like a valid email address.")]
    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
    public bool HasProfilePicture { get; set; }
}

public class EventFormModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Please enter the event name."), StringLength(150, ErrorMessage = "The name can be at most 150 characters.")]
    [Display(Name = "Event name")]
    public string EventName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose the event date."), DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Please enter the location."), StringLength(150, ErrorMessage = "The location can be at most 150 characters.")]
    public string Location { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "The description can be at most 1000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Please choose an event type.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    [Required(ErrorMessage = "Please enter the distance in kilometres."), Range(0.01, 1000, ErrorMessage = "The distance must be between 0.01 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }

    public bool HasBanner { get; set; }
}

public class CategoryFormModel
{
    public int? CategoryId { get; set; }
    public int EventId { get; set; }

    [Required(ErrorMessage = "Please enter a category name, for example 10km or Under 20."), StringLength(100, ErrorMessage = "The name can be at most 100 characters.")]
    [Display(Name = "Category name")]
    public string CategoryName { get; set; } = string.Empty;

    [Range(0.01, 1000, ErrorMessage = "The distance must be between 0.01 and 1000 km.")]
    [Display(Name = "Distance (km) - leave empty for age categories")]
    public decimal? DistanceKm { get; set; }

    [Range(1, 100000, ErrorMessage = "Maximum participants must be between 1 and 100000.")]
    [Display(Name = "Maximum participants")]
    public int MaxParticipants { get; set; } = 100;

    [Range(0, 100000, ErrorMessage = "The entry fee must be between 0 and 100000.")]
    [Display(Name = "Entry fee (R)")]
    public decimal EntryFee { get; set; }
}

public class RouteFormModel
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }

    [Required(ErrorMessage = "Please enter the start point."), StringLength(150)]
    [Display(Name = "Start point")]
    public string StartPoint { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter the end point."), StringLength(150)]
    [Display(Name = "End point")]
    public string EndPoint { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Route description")]
    public string? RouteDescription { get; set; }

    [Url(ErrorMessage = "Please enter a full web address, for example https://maps.example.com/route"), StringLength(255)]
    [Display(Name = "Map link")]
    public string? MapUrl { get; set; }
}

// ---- Page models ----

public class HomeIndexViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public string? Location { get; set; }
    public string? EventType { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}

public class EventDetailsViewModel
{
    public EventDetailDto Event { get; set; } = null!;
    public WeatherDto? Weather { get; set; }
    public Dictionary<int, RouteDto> Routes { get; set; } = new();
    public EnrolmentDto? MyEnrolment { get; set; }
}

public class CategoriesViewModel
{
    public EventDetailDto Event { get; set; } = null!;
    public CategoryFormModel NewCategory { get; set; } = new();
}

public class EnrolmentsViewModel
{
    public EventDetailDto Event { get; set; } = null!;
    public List<EnrolmentDto> Enrolments { get; set; } = new();
}
