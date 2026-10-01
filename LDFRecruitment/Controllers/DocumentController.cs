using System.Security.Claims;
using LDFRecruitment.Data;
using LDFRecruitment.Models;
using LDFRecruitment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LDFRecruitment.Controllers;

/// <summary>Serves uploaded files only to their owner or to staff (non-functional requirement: privacy).</summary>
[Authorize]
public class DocumentController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly FileStorageService _files;

    public DocumentController(ApplicationDbContext db, FileStorageService files)
    {
        _db = db;
        _files = files;
    }

    public async Task<IActionResult> Show(int id)
    {
        var doc = await _db.Documents.AsNoTracking()
            .Include(d => d.Application!).ThenInclude(a => a.Applicant)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();

        var isStaff = User.IsInRole(Roles.Officer) || User.IsInRole(Roles.Administrator);
        var isOwner = doc.Application?.Applicant?.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!isStaff && !isOwner) return Forbid();

        var path = _files.GetFullPath(doc.StoredPath);
        if (!System.IO.File.Exists(path)) return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(path, doc.ContentType);
    }
}
