using System.Security.Claims;
using LDFRecruitment.Data;
using LDFRecruitment.Models;
using LDFRecruitment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace LDFRecruitment.Controllers;

[Authorize(Roles = Roles.Applicant)]
public class ApplicationController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly FileStorageService _files;
    private readonly CertificateExtractionService _extraction;
    private readonly AssessmentService _assessment;
    private readonly ExtractionOptions _extractionOpt;

    public ApplicationController(
        ApplicationDbContext db,
        FileStorageService files,
        CertificateExtractionService extraction,
        AssessmentService assessment,
        IOptions<ExtractionOptions> extractionOpt)
    {
        _db = db;
        _files = files;
        _extraction = extraction;
        _assessment = assessment;
        _extractionOpt = extractionOpt.Value;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private async Task<Applicant> GetApplicantAsync()
    {
        var applicant = await _db.Applicants.FirstOrDefaultAsync(a => a.UserId == UserId);
        if (applicant == null)
        {
            applicant = new Applicant { UserId = UserId, FullName = User.Identity?.Name ?? "" };
            _db.Applicants.Add(applicant);
            await _db.SaveChangesAsync();
        }
        return applicant;
    }

    private async Task<Application> GetApplicationAsync(Applicant applicant)
    {
        var app = await _db.Applications
            .Include(a => a.Documents)
            .Include(a => a.History)
            .Include(a => a.TestResult)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync(a => a.ApplicantId == applicant.Id);

        if (app == null)
        {
            app = new Application { ApplicantId = applicant.Id };
            app.History.Add(new ApplicationStatusHistory { Status = ApplicationStatus.Draft, ChangedBy = "System", Note = "Application started." });
            _db.Applications.Add(app);
            await _db.SaveChangesAsync();
        }
        return app;
    }

    // FR12: application tracking
    public async Task<IActionResult> Index()
    {
        var applicant = await GetApplicantAsync();
        var app = await GetApplicationAsync(applicant);
        return View(new DashboardViewModel
        {
            Applicant = applicant,
            Application = app,
            HasPhoto = app.Documents.Any(d => d.Type == DocumentType.Photo),
            HasCertificate = app.Documents.Any(d => d.Type == DocumentType.Certificate)
        });
    }

    // FR2 / FR3: details form
    [HttpGet]
    public async Task<IActionResult> Details()
    {
        var applicant = await GetApplicantAsync();
        var app = await GetApplicationAsync(applicant);
        if (app.Status != ApplicationStatus.Draft) return RedirectToAction(nameof(Index));

        return View(new ApplicantDetailsViewModel
        {
            FullName = applicant.FullName,
            DateOfBirth = applicant.DateOfBirth,
            Gender = applicant.Gender,
            NationalIdNumber = applicant.NationalIdNumber,
            Citizenship = applicant.Citizenship,
            Phone = applicant.Phone,
            District = applicant.District,
            Address = applicant.Address
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Details(ApplicantDetailsViewModel model)
    {
        var applicant = await GetApplicantAsync();
        var app = await GetApplicationAsync(applicant);
        if (app.Status != ApplicationStatus.Draft) return RedirectToAction(nameof(Index));

        if (model.DateOfBirth.HasValue && model.DateOfBirth.Value.Date > DateTime.UtcNow.Date)
            ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth cannot be in the future.");
        if (!ModelState.IsValid) return View(model);

        applicant.FullName = model.FullName.Trim();
        applicant.DateOfBirth = model.DateOfBirth;
        applicant.Gender = model.Gender;
        applicant.NationalIdNumber = model.NationalIdNumber?.Trim();
        applicant.Citizenship = model.Citizenship;
        applicant.Phone = model.Phone?.Trim();
        applicant.District = model.District;
        applicant.Address = model.Address?.Trim();
        await _db.SaveChangesAsync();

        TempData["Success"] = "Your details were saved.";
        return RedirectToAction(nameof(Index));
    }

    // FR4: photo + certificate upload
    [HttpGet]
    public async Task<IActionResult> Documents()
    {
        var app = await GetApplicationAsync(await GetApplicantAsync());
        if (app.Status != ApplicationStatus.Draft) return RedirectToAction(nameof(Index));
        return View(app);
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Documents(IFormFile? photo, IFormFile? certificate)
    {
        var app = await GetApplicationAsync(await GetApplicantAsync());
        if (app.Status != ApplicationStatus.Draft) return RedirectToAction(nameof(Index));

        var errors = new List<string>();
        if (photo != null) await StoreAsync(app, photo, DocumentType.Photo, errors);
        if (certificate != null) await StoreAsync(app, certificate, DocumentType.Certificate, errors);
        await _db.SaveChangesAsync();

        if (errors.Count > 0) TempData["Error"] = string.Join(" ", errors);
        else if (photo != null || certificate != null) TempData["Success"] = "Documents uploaded.";
        return RedirectToAction(nameof(Documents));
    }

    private async Task StoreAsync(Application app, IFormFile file, DocumentType type, List<string> errors)
    {
        var (stored, error) = await _files.SaveAsync(file, app.Id, type);
        if (stored == null)
        {
            errors.Add($"{type}: {error}");
            return;
        }

        var old = app.Documents.FirstOrDefault(d => d.Type == type);
        if (old != null)
        {
            _files.Delete(old.StoredPath);
            _db.Documents.Remove(old);
            app.Documents.Remove(old);
        }

        var doc = new Document
        {
            ApplicationId = app.Id,
            Type = type,
            OriginalFileName = stored.OriginalName.Length > 260 ? stored.OriginalName[..260] : stored.OriginalName,
            StoredPath = stored.RelativePath,
            ContentType = stored.ContentType
        };
        _db.Documents.Add(doc);
        app.Documents.Add(doc);
    }

    // Submit: validation (FR5) -> OCR/NLP extraction (FR6a/b) -> rule-based checks (FR6)
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit()
    {
        var applicant = await GetApplicantAsync();
        var app = await GetApplicationAsync(applicant);
        if (app.Status != ApplicationStatus.Draft) return RedirectToAction(nameof(Index));

        if (!applicant.DetailsComplete)
        {
            TempData["Error"] = "Please complete your personal details before submitting.";
            return RedirectToAction(nameof(Index));
        }
        var cert = app.Documents.FirstOrDefault(d => d.Type == DocumentType.Certificate);
        if (cert == null || app.Documents.All(d => d.Type != DocumentType.Photo))
        {
            TempData["Error"] = "Please upload both your photo and your certificate before submitting.";
            return RedirectToAction(nameof(Index));
        }

        var extraction = await _extraction.ExtractAsync(_files.GetFullPath(cert.StoredPath), cert.ContentType, cert.OriginalFileName);
        app.ExtractionError = extraction.Success ? null : extraction.Error;

        var oldGrades = await _db.SubjectGrades.Where(g => g.ApplicationId == app.Id).ToListAsync();
        _db.SubjectGrades.RemoveRange(oldGrades);
        foreach (var s in extraction.Subjects)
        {
            _db.SubjectGrades.Add(new SubjectGrade
            {
                ApplicationId = app.Id,
                Subject = s.Subject.Length > 100 ? s.Subject[..100] : s.Subject,
                Grade = s.Grade.Length > 5 ? s.Grade[..5] : s.Grade,
                Confidence = Math.Clamp(s.Confidence, 0, 1),
                NeedsReview = s.Confidence < _extractionOpt.ConfidenceThreshold
            });
        }

        app.Status = ApplicationStatus.Submitted;
        app.SubmittedAt = DateTime.UtcNow;
        _db.StatusHistory.Add(new ApplicationStatusHistory
        {
            ApplicationId = app.Id,
            Status = ApplicationStatus.Submitted,
            ChangedBy = applicant.FullName,
            Note = "Application submitted."
        });
        await _db.SaveChangesAsync();

        await _assessment.RunAsync(app.Id);

        TempData["Success"] = "Application submitted. You can now take the written test.";
        return RedirectToAction(nameof(Index));
    }
}
