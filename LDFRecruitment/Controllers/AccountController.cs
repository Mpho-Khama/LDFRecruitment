using LDFRecruitment.Data;
using LDFRecruitment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LDFRecruitment.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<IdentityUser> _users;
    private readonly SignInManager<IdentityUser> _signIn;
    private readonly ApplicationDbContext _db;

    public AccountController(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn, ApplicationDbContext db)
    {
        _users = users;
        _signIn = signIn;
        _db = db;
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new IdentityUser { UserName = model.Email, Email = model.Email, EmailConfirmed = true };
        var result = await _users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(model);
        }

        await _users.AddToRoleAsync(user, Roles.Applicant);
        _db.Applicants.Add(new Applicant { UserId = user.Id, FullName = model.FullName.Trim() });
        await _db.SaveChangesAsync();

        await _signIn.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Application");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var result = await _signIn.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);

            var user = await _users.FindByEmailAsync(model.Email);
            if (user != null && await _users.IsInRoleAsync(user, Roles.Administrator)) return RedirectToAction("Users", "Admin");
            if (user != null && await _users.IsInRoleAsync(user, Roles.Officer)) return RedirectToAction("Index", "Officer");
            return RedirectToAction("Index", "Application");
        }

        ModelState.AddModelError("", result.IsLockedOut
            ? "Account locked after too many failed attempts. Try again later."
            : "Invalid email or password.");
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();
}
