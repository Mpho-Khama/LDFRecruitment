namespace LDFRecruitment.Services;

public class EligibilityOptions
{
    public int MinAge { get; set; } = 18;
    public int MaxAge { get; set; } = 26;
    public string RequiredCitizenship { get; set; } = "Mosotho";
    public int MinCredits { get; set; } = 5;
    public bool RequireEnglishAndMaths { get; set; } = true;
}

public class ExtractionOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8001/";
    public int TimeoutSeconds { get; set; } = 60;
    public double ConfidenceThreshold { get; set; } = 0.75;
}

public class WrittenTestOptions
{
    public int QuestionCount { get; set; } = 12;
}
