using LDFRecruitment.Data;
using LDFRecruitment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LDFRecruitment.Controllers;

/// <summary>FR13: manage users and the written-test question bank.</summary>
[Authorize(Roles = Roles.Administrator)]
public class AdminController : Controller
{
    private readonly UserManager<IdentityUser> _users;
    private readonly ApplicationDbContext _db;

    public AdminController(UserManager<IdentityUser> users, ApplicationDbContext db)
    {
        _users = users;
        _db = db;
    }

    public async Task<IActionResult> Users()
    {
        var list = new List<UserListItem>();
        foreach (var u in await _users.Users.OrderBy(u => u.Email).ToListAsync())
        {
            list.Add(new UserListItem
            {
                Id = u.Id,
                Email = u.Email ?? u.UserName ?? "",
                Roles = string.Join(", ", await _users.GetRolesAsync(u)),
                IsLocked = await _users.IsLockedOutAsync(u)
            });
        }
        return View(list);
    }

    [HttpGet]
    public IActionResult CreateOfficer() => View(new CreateOfficerViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOfficer(CreateOfficerViewModel model)
    {
        if (model.Role != Roles.Officer && model.Role != Roles.Administrator)
            ModelState.AddModelError(nameof(model.Role), "Invalid role.");
        if (!ModelState.IsValid) return View(model);

        var user = new IdentityUser { UserName = model.Email, Email = model.Email, EmailConfirmed = true };
        var result = await _users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(model);
        }
        await _users.AddToRoleAsync(user, model.Role);
        TempData["Success"] = "Account created.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();
        if (user.UserName == User.Identity?.Name)
        {
            TempData["Error"] = "You cannot lock your own account.";
            return RedirectToAction(nameof(Users));
        }

        if (await _users.IsLockedOutAsync(user))
            await _users.SetLockoutEndDateAsync(user, null);
        else
            await _users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> Questions() =>
        View(await _db.Questions.AsNoTracking().OrderBy(q => q.Category).ThenBy(q => q.Id).ToListAsync());

    [HttpGet]
    public IActionResult CreateQuestion() => View(new Question());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateQuestion(Question model)
    {
        model.Id = 0;
        model.CorrectOption = (model.CorrectOption ?? "").Trim().ToUpperInvariant();
        if (model.CorrectOption is not ("A" or "B" or "C" or "D"))
            ModelState.AddModelError(nameof(model.CorrectOption), "Correct option must be A, B, C or D.");
        if (!ModelState.IsValid) return View(model);

        _db.Questions.Add(model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Questions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion(int id)
    {
        var q = await _db.Questions.FindAsync(id);
        if (q != null)
        {
            _db.Questions.Remove(q);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Questions));
    }
}
