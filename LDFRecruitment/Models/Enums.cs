namespace LDFRecruitment.Models;

public static class Roles
{
    public const string Applicant = "Applicant";
    public const string Officer = "RecruitmentOfficer";
    public const string Administrator = "Administrator";
    public const string Staff = Officer + "," + Administrator;
}

public enum ApplicationStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    Shortlisted = 3,
    NotSelected = 4
}

public enum DocumentType
{
    Photo = 0,
    Certificate = 1
}
