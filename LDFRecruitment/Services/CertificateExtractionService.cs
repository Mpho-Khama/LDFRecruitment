using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LDFRecruitment.Services;

public record ExtractedSubject(
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("grade")] string Grade,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("raw_line")] string? RawLine);

public class ExtractionResponse
{
    [JsonPropertyName("subjects")] public List<ExtractedSubject> Subjects { get; set; } = new();
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = new();
}

public record ExtractionResult(bool Success, List<ExtractedSubject> Subjects, string? Error);

/// <summary>
/// Calls the separate Python FastAPI OCR + NLP service over HTTP (Methodology 3.11.1 / 3.14).
/// If the service is down the application still works: the officer is told to capture grades manually.
/// </summary>
public class CertificateExtractionService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly ILogger<CertificateExtractionService> _log;

    public CertificateExtractionService(HttpClient http, ILogger<CertificateExtractionService> log)
    {
        _http = http;
        _log = log;
    }

    public async Task<ExtractionResult> ExtractAsync(string filePath, string contentType, string fileName, CancellationToken ct = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            await using var stream = File.OpenRead(filePath);
            var file = new StreamContent(stream);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(file, "file", fileName);

            using var response = await _http.PostAsync("extract", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var msg = $"Extraction service returned {(int)response.StatusCode}.";
                _log.LogWarning(msg);
                return new ExtractionResult(false, new(), msg);
            }

            var body = await response.Content.ReadFromJsonAsync<ExtractionResponse>(Json, ct)
                       ?? new ExtractionResponse();
            return new ExtractionResult(true, body.Subjects, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _log.LogWarning(ex, "Certificate extraction service unavailable.");
            return new ExtractionResult(false, new(), "Automatic extraction is unavailable; an officer must capture the grades.");
        }
    }
}
