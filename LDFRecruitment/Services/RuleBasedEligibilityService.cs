using LDFRecruitment.Models;
using Microsoft.Extensions.Options;

namespace LDFRecruitment.Services;

/// <summary>
/// Predefined, transparent eligibility rules (FR6): age, citizenship, document completeness
/// and educational qualifications. No AI is involved here, by design (Methodology 3.5 / literature review).
/// </summary>
public class RuleBasedEligibilityService
{
    private readonly EligibilityOptions _opt;

    public RuleBasedEligibilityService(IOptions<EligibilityOptions> options) => _opt = options.Value;

    public EligibilityAssessment Evaluate(
        Applicant applicant,
        IReadOnlyCollection<Document> documents,
        IReadOnlyCollection<SubjectGrade> grades,
        DateTime today)
    {
        var lines = new List<string>();
        var result = new EligibilityAssessment { AssessedAt = DateTime.UtcNow };

        // Age
        if (applicant.DateOfBirth.HasValue)
        {
            var age = CalculateAge(applicant.DateOfBirth.Value, today);
            result.AgeAtAssessment = age;
            result.AgeOk = age >= _opt.MinAge && age <= _opt.MaxAge;
            lines.Add($"Age {age}: {(result.AgeOk ? "meets" : "does not meet")} the requirement of {_opt.MinAge}-{_opt.MaxAge}.");
        }
        else
        {
            lines.Add("Date of birth missing.");
        }

        // Citizenship
        result.CitizenshipOk = string.Equals(applicant.Citizenship?.Trim(), _opt.RequiredCitizenship, StringComparison.OrdinalIgnoreCase);
        lines.Add($"Citizenship '{applicant.Citizenship ?? "-"}': {(result.CitizenshipOk ? "meets" : "does not meet")} the requirement ({_opt.RequiredCitizenship}).");

        // Documents
        var hasPhoto = documents.Any(d => d.Type == DocumentType.Photo);
        var hasCert = documents.Any(d => d.Type == DocumentType.Certificate);
        result.DocumentsComplete = hasPhoto && hasCert;
        lines.Add(result.DocumentsComplete
            ? "Photo and certificate uploaded."
            : $"Missing document(s):{(hasPhoto ? "" : " photo")}{(hasCert ? "" : " certificate")}.");

        // Education (from extracted / officer-verified grades)
        var credits = grades.Where(g => IsCredit(g.Grade)).ToList();
        result.CreditCount = credits.Count;
        var englishOk = credits.Any(g => IsEnglish(g.Subject));
        var mathsOk = credits.Any(g => IsMaths(g.Subject));
        result.EducationOk = credits.Count >= _opt.MinCredits && (!_opt.RequireEnglishAndMaths || (englishOk && mathsOk));
        result.UsesUnverifiedGrades = grades.Any(g => !g.IsVerified);

        if (grades.Count == 0)
            lines.Add("No grades available yet - officer verification of the certificate is required.");
        else
            lines.Add($"{credits.Count} credit pass(es) (minimum {_opt.MinCredits})"
                + (_opt.RequireEnglishAndMaths ? $"; English credit: {(englishOk ? "yes" : "no")}; Mathematics credit: {(mathsOk ? "yes" : "no")}." : "."));
        if (result.UsesUnverifiedGrades && grades.Count > 0)
            lines.Add("Some grades are not yet verified by an officer.");

        result.IsEligible = result.AgeOk && result.CitizenshipOk && result.DocumentsComplete && result.EducationOk;
        result.Summary = string.Join(" ", lines);
        if (result.Summary.Length > 1500) result.Summary = result.Summary[..1500];
        return result;
    }

    public static int CalculateAge(DateTime dob, DateTime today)
    {
        var age = today.Year - dob.Year;
        if (dob.Date > today.Date.AddYears(-age)) age--;
        return age;
    }

    /// <summary>Credit = LGCSE A*-C or COSC grades 1-6.</summary>
    public static bool IsCredit(string? grade)
    {
        if (string.IsNullOrWhiteSpace(grade)) return false;
        var g = grade.Trim().ToUpperInvariant();
        return g is "A*" or "A" or "B" or "C" or "1" or "2" or "3" or "4" or "5" or "6";
    }

    private static bool IsEnglish(string subject) =>
        subject.Contains("ENGLISH", StringComparison.OrdinalIgnoreCase)
        && !subject.Contains("LITERATURE", StringComparison.OrdinalIgnoreCase);

    private static bool IsMaths(string subject) =>
        subject.Contains("MATHEMATICS", StringComparison.OrdinalIgnoreCase)
        && !subject.Contains("ADDITIONAL", StringComparison.OrdinalIgnoreCase);
}
