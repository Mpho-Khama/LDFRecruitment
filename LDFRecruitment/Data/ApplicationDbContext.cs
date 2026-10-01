using LDFRecruitment.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LDFRecruitment.Data;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<EligibilityAssessment> EligibilityAssessments => Set<EligibilityAssessment>();
    public DbSet<SubjectGrade> SubjectGrades => Set<SubjectGrade>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<TestResult> TestResults => Set<TestResult>();
    public DbSet<ApplicationStatusHistory> StatusHistory => Set<ApplicationStatusHistory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Applicant>().HasIndex(a => a.UserId).IsUnique();

        builder.Entity<Application>()
            .HasOne(a => a.Assessment).WithOne(e => e.Application)
            .HasForeignKey<EligibilityAssessment>(e => e.ApplicationId);

        // One attempt only: at most one TestResult row per application.
        builder.Entity<Application>()
            .HasOne(a => a.TestResult).WithOne(t => t.Application)
            .HasForeignKey<TestResult>(t => t.ApplicationId);
    }
}
