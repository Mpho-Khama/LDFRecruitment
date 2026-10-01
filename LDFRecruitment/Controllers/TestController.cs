using System.Security.Claims;
using LDFRecruitment.Data;
using LDFRecruitment.Models;
using LDFRecruitment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LDFRecruitment.Controllers;

/// <summary>Written test: random fixed-length set (FR7), auto-graded (FR7a), single attempt (FR7b).</summary>
[Authorize(Roles = Roles.Applicant)]
public class TestController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly WrittenTestOptions _opt;

    public TestController(ApplicationDbContext db, IOptions<WrittenTestOptions> opt)
    {
        _db = db;
        _opt = opt.Value;
    }

    private async Task<Application?> GetApplicationAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return await _db.Applications
            .Include(a => a.TestResult)
            .Where(a => a.Applicant!.UserId == userId)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<IActionResult> Index()
    {
        var app = await GetApplicationAsync();
        if (app == null || app.Status == ApplicationStatus.Draft)
        {
            TempData["Error"] = "Submit your application before taking the written test.";
            return RedirectToAction("Index", "Application");
        }
        return View(app);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Start()
    {
        var app = await GetApplicationAsync();
        if (app == null || app.Status == ApplicationStatus.Draft) return RedirectToAction("Index", "Application");

        if (app.TestResult != null) return RedirectToAction(nameof(Take)); // never create a second attempt

        var all = await _db.Questions.AsNoTracking().ToListAsync();
        var count = Math.Min(_opt.QuestionCount, all.Count);
        var rng = new Random();

        // Spread questions across subject categories, then shuffle.
        var groups = all.GroupBy(q => q.Category).Select(g => g.OrderBy(_ => rng.Next()).ToList()).ToList();
        var picked = new List<Question>();
        var round = 0;
        while (picked.Count < count && groups.Any(g => g.Count > round))
        {
            foreach (var g in groups)
            {
                if (picked.Count >= count) break;
                if (g.Count > round) picked.Add(g[round]);
            }
            round++;
        }
        picked = picked.OrderBy(_ => rng.Next()).ToList();

        _db.TestResults.Add(new TestResult
        {
            ApplicationId = app.Id,
            TotalQuestions = picked.Count,
            QuestionIds = string.Join(",", picked.Select(q => q.Id))
        });
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique index on ApplicationId: a parallel request already created the attempt.
        }
        return RedirectToAction(nameof(Take));
    }

    [HttpGet]
    public async Task<IActionResult> Take()
    {
        var app = await GetApplicationAsync();
        var result = app?.TestResult;
        if (result == null || result.IsSubmitted) return RedirectToAction(nameof(Index));

        var ids = result.QuestionIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        var questions = await _db.Questions.AsNoTracking().Where(q => ids.Contains(q.Id)).ToListAsync();
        return View(new TakeTestViewModel
        {
            TestResultId = result.Id,
            Questions = ids.Select(id => questions.FirstOrDefault(q => q.Id == id)).Where(q => q != null).Select(q => q!).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Dictionary<int, string> answers)
    {
        var app = await GetApplicationAsync();
        var result = app?.TestResult;
        if (result == null || result.IsSubmitted) return RedirectToAction(nameof(Index));

        var ids = result.QuestionIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        var questions = await _db.Questions.Where(q => ids.Contains(q.Id)).ToListAsync();

        result.Score = questions.Count(q => answers.TryGetValue(q.Id, out var a) && a == q.CorrectOption);
        result.SubmittedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Your test has been submitted.";
        return RedirectToAction(nameof(Index));
    }
}
