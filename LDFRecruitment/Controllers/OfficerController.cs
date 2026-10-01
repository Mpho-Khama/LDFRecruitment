using LDFRecruitment.Data;
using LDFRecruitment.Models;
using LDFRecruitment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LDFRecruitment.Controllers;

/// <summary>Officer review (FR9-FR11). AI output is decision support only: officers verify and decide.</summary>
[Authorize(Roles = Roles.Staff)]
public class OfficerController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AssessmentService _assessment;
    private readonly ExtractionOptions _extractionOpt;

    public OfficerController(ApplicationDbContext db, AssessmentService assessment, IOptions<ExtractionOptions> extractionOpt)
    {
        _db = db;
        _assessment = assessment;
        _extractionOpt = extractionOpt.Value;
    }

    public async Task<IActionResult> Index(ApplicationStatus? status, string? search, bool flaggedOnly = false)
    {
        var query = _db.Applications.AsNoTracking().Where(a => a.Status != ApplicationStatus.Draft);
        if (status.HasValue) query = query.Where(a => a.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a => a.Applicant!.FullName.Contains(search) || a.Applicant!.NationalIdNumber!.Contains(search));
        if (flaggedOnly) query = query.Where(a => a.Grades.Any(g => g.NeedsReview && !g.IsVerified));

        var rows = await query
            .OrderByDescending(a => a.SubmittedAt)
            .Select(a => new
            {
                a.Id,
                a.Applicant!.FullName,
                a.Status,
                a.SubmittedAt,
                Eligible = a.Assessment != null ? (bool?)a.Assessment.IsEligible : null,
                Flagged = a.Grades.Count(g => g.NeedsReview && !g.IsVerified),
                Test = a.TestResult
            })
            .ToListAsync();

        return View(new OfficerListViewModel
        {
            StatusFilter = status,
            Search = search,
            FlaggedOnly = flaggedOnly,
            Items = rows.Select(r => new OfficerListItem
            {
                ApplicationId = r.Id,
                FullName = r.FullName,
                Status = r.Status,
                SubmittedAt = r.SubmittedAt,
                IsEligible = r.Eligible,
                FlaggedGrades = r.Flagged,
                TestScore = r.Test is { IsSubmitted: true } ? $"{r.Test.Score}/{r.Test.TotalQuestions}" : null
            }).ToList()
        });
    }

    public async Task<IActionResult> Review(int id)
    {
        var app = await LoadAsync(id);
        if (app == null) return NotFound();

        return View(new ReviewViewModel
        {
            Application = app,
            ConfidenceThreshold = _extractionOpt.ConfidenceThreshold,
            PhotoDocumentId = app.Documents.FirstOrDefault(d => d.Type == DocumentType.Photo)?.Id,
            CertificateDocumentId = app.Documents.FirstOrDefault(d => d.Type == DocumentType.Certificate)?.Id
        });
    }

    private Task<Application?> LoadAsync(int id) => _db.Applications
        .Include(a => a.Applicant)
        .Include(a => a.Documents)
        .Include(a => a.Grades)
        .Include(a => a.Assessment)
        .Include(a => a.TestResult)
        .Include(a => a.History)
        .AsSplitQuery()
        .FirstOrDefaultAsync(a => a.Id == id);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyGrade(int gradeId, string subject, string grade)
    {
        var g = await _db.SubjectGrades.FindAsync(gradeId);
        if (g == null) return NotFound();

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(grade) || grade.Trim().Length > 5)
        {
            TempData["Error"] = "Subject and a valid grade are required.";
            return RedirectToAction(nameof(Review), new { id = g.ApplicationId });
        }

        g.Subject = subject.Trim();
        g.Grade = grade.Trim().ToUpperInvariant();
        g.IsVerified = true;
        g.NeedsReview = false;
        await _db.SaveChangesAsync();
        await _assessment.RunAsync(g.ApplicationId);
        return RedirectToAction(nameof(Review), new { id = g.ApplicationId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddGrade(int applicationId, string subject, string grade)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(grade) || grade.Trim().Length > 5)
        {
            TempData["Error"] = "Subject and a valid grade are required.";
            return RedirectToAction(nameof(Review), new { id = applicationId });
        }

        _db.SubjectGrades.Add(new SubjectGrade
        {
            ApplicationId = applicationId,
            Subject = subject.Trim(),
            Grade = grade.Trim().ToUpperInvariant(),
            Confidence = 1,
            IsVerified = true
        });
        await _db.SaveChangesAsync();
        await _assessment.RunAsync(applicationId);
        return RedirectToAction(nameof(Review), new { id = applicationId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGrade(int gradeId)
    {
        var g = await _db.SubjectGrades.FindAsync(gradeId);
        if (g == null) return NotFound();
        var appId = g.ApplicationId;
        _db.SubjectGrades.Remove(g);
        await _db.SaveChangesAsync();
        await _assessment.RunAsync(appId);
        return RedirectToAction(nameof(Review), new { id = appId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, ApplicationStatus status, string? note)
    {
        var app = await _db.Applications.FindAsync(id);
        if (app == null) return NotFound();
        if (status == ApplicationStatus.Draft)
        {
            TempData["Error"] = "An application cannot be moved back to draft.";
            return RedirectToAction(nameof(Review), new { id });
        }

        app.Status = status;
        if (!string.IsNullOrWhiteSpace(note)) app.OfficerNotes = note.Trim();
        _db.StatusHistory.Add(new ApplicationStatusHistory
        {
            ApplicationId = id,
            Status = status,
            ChangedBy = User.Identity?.Name ?? "Officer",
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Status updated.";
        return RedirectToAction(nameof(Review), new { id });
    }
}
