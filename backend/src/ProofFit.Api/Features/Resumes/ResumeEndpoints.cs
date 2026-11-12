using Microsoft.AspNetCore.Http.HttpResults;
using ProofFit.Api.Data;
using ProofFit.Api.Domain;

namespace ProofFit.Api.Features.Resumes;

public record ResumeResponse(Guid Id, string FileName, int PageCount, string Text, DateTimeOffset UploadedAt)
{
    public static ResumeResponse From(Resume resume) =>
        new(resume.Id, resume.FileName, resume.PageCount, resume.Text, resume.UploadedAt);
}

public static class ResumeEndpoints
{
    public static IEndpointRouteBuilder MapResumeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/resumes").WithTags("Resumes");

        // Antiforgery is off because there is no cookie session to forge yet. Revisit when
        // GitHub sign-in adds one.
        group.MapPost("/", Upload).DisableAntiforgery();
        group.MapGet("/{id:guid}", Get).WithName("GetResume");

        return app;
    }

    private static async Task<Results<CreatedAtRoute<ResumeResponse>, ValidationProblem>> Upload(
        IFormFile file, PdfTextExtractor extractor, AppDbContext db, CancellationToken ct)
    {
        if (file.Length == 0)
            return Invalid("The file is empty.");
        if (file.Length > Limits.ResumeFileBytes)
            return Invalid($"The file is larger than {Limits.ResumeFileBytes / (1024 * 1024)} MB.");

        PdfText pdf;
        try
        {
            await using var stream = file.OpenReadStream();
            pdf = extractor.Extract(stream);
        }
        catch (PdfReadException ex)
        {
            return Invalid(ex.Message);
        }

        if (pdf.Text.Length == 0)
            return Invalid("The PDF has no selectable text. A scanned resume has to be exported as text first.");

        var resume = new Resume
        {
            Id = Guid.CreateVersion7(),
            FileName = DisplayName(file.FileName),
            Text = pdf.Text,
            PageCount = pdf.PageCount,
            UploadedAt = Timestamp.Now(),
        };
        db.Resumes.Add(resume);
        await db.SaveChangesAsync(ct);

        return TypedResults.CreatedAtRoute(ResumeResponse.From(resume), "GetResume", new { id = resume.Id });
    }

    private static async Task<Results<Ok<ResumeResponse>, NotFound>> Get(Guid id, AppDbContext db, CancellationToken ct)
    {
        var resume = await db.Resumes.FindAsync([id], ct);
        return resume is null ? TypedResults.NotFound() : TypedResults.Ok(ResumeResponse.From(resume));
    }

    private static ValidationProblem Invalid(string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [message] });

    // The client-supplied name is only ever shown back to the user, never used as a path.
    private static string DisplayName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        return name.Length <= Limits.FileNameLength ? name : name[..Limits.FileNameLength];
    }
}
