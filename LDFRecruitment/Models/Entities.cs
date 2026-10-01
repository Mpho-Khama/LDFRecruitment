using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace LDFRecruitment.Models;

/// <summary>Personal profile linked 1:1 to an Identity user (ERD: Applicant / UserAccount).</summary>
public class Applicant
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = "";
    public IdentityUser? User { get; set; }

    [Required, StringLength(120)]
    public string FullName { get; set; } = "";

    public DateTime? DateOfBirth { get; set; }
    [StringLength(10)] public string? Gender { get; set; }
    [StringLength(30)] public string? NationalIdNumber { get; set; }
    [StringLength(40)] public string? Citizenship { get; set; }
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(40)] public string? District { get; set; }
    [StringLength(250)] public string? Address { get; set; }

    public ICollection<Application> Applications { get; set; } = new List<Application>();

    public bool DetailsComplete =>
        DateOfBirth.HasValue
        && !string.IsNullOrWhiteSpace(Gender)
        && !string.IsNullOrWhiteSpace(NationalIdNumber)
        && !string.IsNullOrWhiteSpace(Citizenship)
        && !string.IsNullOrWhiteSpace(Phone)
        && !string.IsNullOrWhiteSpace(District);
}

public class Application
{
    public int Id { get; set; }
    public int ApplicantId { get; set; }
    public Applicant? Applicant { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }

    [StringLength(1000)]
    public string? OfficerNotes { get; set; }

    /// <summary>Set when the certificate OCR/NLP service could not be reached or failed.</summary>
    [StringLength(300)]
    public string? ExtractionError { get; set; }

    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<SubjectGrade> Grades { get; set; } = new List<SubjectGrade>();
    public ICollection<ApplicationStatusHistory> History { get; set; } = new List<ApplicationStatusHistory>();
    public EligibilityAssessment? Assessment { get; set; }
    public TestResult? TestResult { get; set; }
}

public class Document
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public Application? Application { get; set; }

    public DocumentType Type { get; set; }
    [StringLength(260)] public string OriginalFileName { get; set; } = "";
    /// <summary>Path relative to App_Data/uploads.</summary>
    [StringLength(400)] public string StoredPath { get; set; } = "";
    [StringLength(100)] public string ContentType { get; set; } = "";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Result of the predefined rule-based checks (ERD: EligibilityAssessment / AssessmentResult).</summary>
public class EligibilityAssessment
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public Application? Application { get; set; }

    public int? AgeAtAssessment { get; set; }
    public bool AgeOk { get; set; }
    public bool CitizenshipOk { get; set; }
    public bool DocumentsComplete { get; set; }
    public bool EducationOk { get; set; }
    public int CreditCount { get; set; }
    public bool IsEligible { get; set; }
    /// <summary>True when at least one grade used by the rules has not been verified by an officer.</summary>
    public bool UsesUnverifiedGrades { get; set; }

    [StringLength(1500)] public string Summary { get; set; } = "";
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One subject/grade pair extracted from the certificate by the OCR + NLP service.</summary>
public class SubjectGrade
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public Application? Application { get; set; }

    [Required, StringLength(100)] public string Subject { get; set; } = "";
    [Required, StringLength(5)] public string Grade { get; set; } = "";
    /// <summary>0..1 confidence from the extraction pipeline.</summary>
    public double Confidence { get; set; }
    /// <summary>True when confidence is below the threshold and an officer must check it.</summary>
    public bool NeedsReview { get; set; }
    public bool IsVerified { get; set; }
}

public class Question
{
    public int Id { get; set; }
    [Required, StringLength(40)] public string Category { get; set; } = "";
    [Required, StringLength(500)] public string Text { get; set; } = "";
    [Required, StringLength(200)] public string OptionA { get; set; } = "";
    [Required, StringLength(200)] public string OptionB { get; set; } = "";
    [Required, StringLength(200)] public string OptionC { get; set; } = "";
    [Required, StringLength(200)] public string OptionD { get; set; } = "";
    /// <summary>"A", "B", "C" or "D".</summary>
    [Required, StringLength(1)] public string CorrectOption { get; set; } = "A";
}

public class TestResult
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public Application? Application { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public int Score { get; set; }
    public int TotalQuestions { get; set; }
    /// <summary>Comma-separated question ids in the order presented.</summary>
    [StringLength(500)] public string QuestionIds { get; set; } = "";

    public bool IsSubmitted => SubmittedAt.HasValue;
}

public class ApplicationStatusHistory
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public Application? Application { get; set; }

    public ApplicationStatus Status { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    [StringLength(256)] public string ChangedBy { get; set; } = "";
    [StringLength(500)] public string? Note { get; set; }
}
