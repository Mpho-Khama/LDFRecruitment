using System.ComponentModel.DataAnnotations;

namespace LDFRecruitment.Models;

public class RegisterViewModel
{
    [Required, StringLength(120), Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = "";

    [DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match."), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";
}

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}

public class ApplicantDetailsViewModel
{
    [Required, StringLength(120), Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required, DataType(DataType.Date), Display(Name = "Date of birth")]
    public DateTime? DateOfBirth { get; set; }

    [Required]
    public string? Gender { get; set; }

    [Required, StringLength(30), Display(Name = "National ID / Passport number")]
    public string? NationalIdNumber { get; set; }

    [Required]
    public string? Citizenship { get; set; }

    [Required, Phone, StringLength(30), Display(Name = "Phone number")]
    public string? Phone { get; set; }

    [Required]
    public string? District { get; set; }

    [StringLength(250), Display(Name = "Residential address")]
    public string? Address { get; set; }

    public static readonly string[] Districts =
    {
        "Berea", "Butha-Buthe", "Leribe", "Mafeteng", "Maseru",
        "Mohale's Hoek", "Mokhotlong", "Qacha's Nek", "Quthing", "Thaba-Tseka"
    };
}

public class DashboardViewModel
{
    public Application Application { get; set; } = null!;
    public Applicant Applicant { get; set; } = null!;
    public bool HasPhoto { get; set; }
    public bool HasCertificate { get; set; }
}

public class TakeTestViewModel
{
    public int TestResultId { get; set; }
    public List<Question> Questions { get; set; } = new();
}

public class OfficerListItem
{
    public int ApplicationId { get; set; }
    public string FullName { get; set; } = "";
    public ApplicationStatus Status { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public bool? IsEligible { get; set; }
    public int FlaggedGrades { get; set; }
    public string? TestScore { get; set; }
}

public class OfficerListViewModel
{
    public List<OfficerListItem> Items { get; set; } = new();
    public ApplicationStatus? StatusFilter { get; set; }
    public string? Search { get; set; }
    public bool FlaggedOnly { get; set; }
}

public class ReviewViewModel
{
    public Application Application { get; set; } = null!;
    public double ConfidenceThreshold { get; set; }
    public int? PhotoDocumentId { get; set; }
    public int? CertificateDocumentId { get; set; }
}

public class CreateOfficerViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = "";

    [Required]
    public string Role { get; set; } = Roles.Officer;
}

public class UserListItem
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string Roles { get; set; } = "";
    public bool IsLocked { get; set; }
}
