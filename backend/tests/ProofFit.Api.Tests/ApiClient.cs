using System.Net.Http.Headers;
using System.Net.Http.Json;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses;
using ProofFit.Api.Features.Resumes;
using ProofFit.Api.Features.Vacancies;

namespace ProofFit.Api.Tests;

/// <summary>The steps a browser takes, for tests that need a resume, a vacancy or an analysis in place.</summary>
public static class ApiClient
{
    public static Task<HttpResponseMessage> PostResumeAsync(this HttpClient client, byte[] content, string fileName)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return client.PostAsync("/api/resumes", new MultipartFormDataContent { { file, "file", fileName } });
    }

    public static async Task<ResumeResponse> UploadResumeAsync(this HttpClient client, params string[] lines)
    {
        var response = await client.PostResumeAsync(TestPdf.WithLines(lines), "resume.pdf");
        return await response.ReadAsync<ResumeResponse>();
    }

    public static async Task<VacancyResponse> CreateVacancyAsync(this HttpClient client, string text)
    {
        var response = await client.PostAsJsonAsync("/api/vacancies", new { title = "Full-stack developer", text });
        return await response.ReadAsync<VacancyResponse>();
    }

    public static async Task<AnalysisResponse> StartAnalysisAsync(this HttpClient client, Guid resumeId, Guid vacancyId)
    {
        var response = await client.PostAsJsonAsync("/api/analyses", new { resumeId, vacancyId });
        return await response.ReadAsync<AnalysisResponse>();
    }

    /// <summary>Polls until the analysis has completed or failed.</summary>
    public static async Task<AnalysisResponse> WaitForAnalysisAsync(this HttpClient client, Guid id)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (true)
        {
            var analysis = await client.GetFromJsonAsync<AnalysisResponse>($"/api/analyses/{id}", ApiJson.Options, timeout.Token);
            if (analysis!.Status is AnalysisStatus.Completed or AnalysisStatus.Failed) return analysis;
            await Task.Delay(50, timeout.Token);
        }
    }

    /// <summary>Reads a successful response body with the API's own JSON settings.</summary>
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var value = await response.Content.ReadFromJsonAsync<T>(ApiJson.Options);
        Assert.NotNull(value);
        return value;
    }
}
