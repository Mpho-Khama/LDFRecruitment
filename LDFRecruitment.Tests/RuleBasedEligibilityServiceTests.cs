using LDFRecruitment.Models;
using LDFRecruitment.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace LDFRecruitment.Tests;

public class RuleBasedEligibilityServiceTests
{
    private static readonly DateTime Today = new(2026, 10, 1);

    private static RuleBasedEligibilityService Service() =>
        new(Options.Create(new EligibilityOptions()));

    private static Applicant Applicant(DateTime dob, string citizenship = "Mosotho") =>
        new() { FullName = "Test", DateOfBirth = dob, Citizenship = citizenship };

    private static List<Document> Docs() => new()
    {
        new Document { Type = DocumentType.Photo },
        new Document { Type = DocumentType.Certificate }
    };

    private static List<SubjectGrade> Grades(params (string subject, string grade)[] items) =>
        items.Select(i => new SubjectGrade { Subject = i.subject, Grade = i.grade, IsVerified = true }).ToList();

    private static List<SubjectGrade> GoodGrades() => Grades(
        ("ENGLISH LANGUAGE", "B"), ("MATHEMATICS", "C"), ("SESOTHO", "A"),
        ("BIOLOGY", "C"), ("GEOGRAPHY", "B"));

    [Fact]
    public void EligibleApplicant_MeetsAllRules()
    {
        var r = Service().Evaluate(Applicant(new DateTime(2004, 5, 10)), Docs(), GoodGrades(), Today);
        Assert.True(r.AgeOk);
        Assert.True(r.CitizenshipOk);
        Assert.True(r.DocumentsComplete);
        Assert.True(r.EducationOk);
        Assert.True(r.IsEligible);
    }

    [Theory]
    [InlineData(2010, 1, 1, false)]  // 16 - too young
    [InlineData(2008, 10, 1, true)]  // exactly 18 today
    [InlineData(2008, 10, 2, false)] // turns 18 tomorrow
    [InlineData(1990, 1, 1, false)]  // too old
    public void Age_BoundaryChecks(int y, int m, int d, bool expected)
    {
        var r = Service().Evaluate(Applicant(new DateTime(y, m, d)), Docs(), GoodGrades(), Today);
        Assert.Equal(expected, r.AgeOk);
    }

    [Fact]
    public void NonMosotho_FailsCitizenship()
    {
        var r = Service().Evaluate(Applicant(new DateTime(2004, 1, 1), "Other"), Docs(), GoodGrades(), Today);
        Assert.False(r.CitizenshipOk);
        Assert.False(r.IsEligible);
    }

    [Fact]
    public void MissingCertificate_FailsDocumentCheck()
    {
        var docs = new List<Document> { new() { Type = DocumentType.Photo } };
        var r = Service().Evaluate(Applicant(new DateTime(2004, 1, 1)), docs, GoodGrades(), Today);
        Assert.False(r.DocumentsComplete);
        Assert.False(r.IsEligible);
    }

    [Fact]
    public void TooFewCredits_FailsEducation()
    {
        var grades = Grades(("ENGLISH LANGUAGE", "B"), ("MATHEMATICS", "C"), ("SESOTHO", "D"), ("BIOLOGY", "E"), ("GEOGRAPHY", "F"));
        var r = Service().Evaluate(Applicant(new DateTime(2004, 1, 1)), Docs(), grades, Today);
        Assert.Equal(2, r.CreditCount);
        Assert.False(r.EducationOk);
    }

    [Fact]
    public void MissingMathsCredit_FailsWhenRequired()
    {
        var grades = Grades(("ENGLISH LANGUAGE", "B"), ("MATHEMATICS", "E"), ("SESOTHO", "A"), ("BIOLOGY", "C"), ("GEOGRAPHY", "B"), ("HISTORY", "C"));
        var r = Service().Evaluate(Applicant(new DateTime(2004, 1, 1)), Docs(), grades, Today);
        Assert.True(r.CreditCount >= 5);
        Assert.False(r.EducationOk);
    }

    [Fact]
    public void NoGrades_FailsEducationAndFlagsUnverified()
    {
        var r = Service().Evaluate(Applicant(new DateTime(2004, 1, 1)), Docs(), new List<SubjectGrade>(), Today);
        Assert.False(r.EducationOk);
        Assert.False(r.IsEligible);
    }

    [Fact]
    public void UnverifiedGrades_AreFlagged()
    {
        var grades = GoodGrades();
        grades[0].IsVerified = false;
        var r = Service().Evaluate(Applicant(new DateTime(2004, 1, 1)), Docs(), grades, Today);
        Assert.True(r.UsesUnverifiedGrades);
    }

    [Theory]
    [InlineData("A*", true)]
    [InlineData("c", true)]
    [InlineData("6", true)]
    [InlineData("7", false)]
    [InlineData("D", false)]
    [InlineData("", false)]
    public void IsCredit_RecognisesLgcseAndCoscGrades(string grade, bool expected) =>
        Assert.Equal(expected, RuleBasedEligibilityService.IsCredit(grade));
}
