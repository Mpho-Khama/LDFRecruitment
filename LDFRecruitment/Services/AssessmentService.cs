using LDFRecruitment.Data;
using LDFRecruitment.Models;
using Microsoft.EntityFrameworkCore;

namespace LDFRecruitment.Services;

/// <summary>Runs the rule engine for an application and stores the result (FR6, FR9).</summary>
public class AssessmentService
{
    private readonly ApplicationDbContext _db;
    private readonly RuleBasedEligibilityService _rules;

    public AssessmentService(ApplicationDbContext db, RuleBasedEligibilityService rules)
    {
        _db = db;
        _rules = rules;
    }

    public async Task RunAsync(int applicationId)
    {
        var app = await _db.Applications
            .Include(a => a.Applicant)
            .Include(a => a.Documents)
            .Include(a => a.Grades)
            .Include(a => a.Assessment)
            .FirstAsync(a => a.Id == applicationId);

        var fresh = _rules.Evaluate(app.Applicant!, app.Documents.ToList(), app.Grades.ToList(), DateTime.UtcNow);

        if (app.Assessment == null)
        {
            fresh.ApplicationId = app.Id;
            _db.EligibilityAssessments.Add(fresh);
        }
        else
        {
            var a = app.Assessment;
            a.AgeAtAssessment = fresh.AgeAtAssessment;
            a.AgeOk = fresh.AgeOk;
            a.CitizenshipOk = fresh.CitizenshipOk;
            a.DocumentsComplete = fresh.DocumentsComplete;
            a.EducationOk = fresh.EducationOk;
            a.CreditCount = fresh.CreditCount;
            a.IsEligible = fresh.IsEligible;
            a.UsesUnverifiedGrades = fresh.UsesUnverifiedGrades;
            a.Summary = fresh.Summary;
            a.AssessedAt = fresh.AssessedAt;
        }
        await _db.SaveChangesAsync();
    }
}
