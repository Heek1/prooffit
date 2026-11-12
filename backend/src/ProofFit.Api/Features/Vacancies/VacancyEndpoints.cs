using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using ProofFit.Api.Data;
using ProofFit.Api.Domain;

namespace ProofFit.Api.Features.Vacancies;

public class CreateVacancyRequest
{
    // Normalising in the setters means the length rules below judge the text that gets
    // stored, not the padding around it.
    [Required, StringLength(Limits.VacancyTitleLength)]
    public string Title { get; set => field = value?.Trim() ?? ""; } = "";

    // Line endings become "\n" so that line numbers, which evidence is cited by, mean the
    // same thing here and in the browser.
    [Required, StringLength(Limits.VacancyTextMaxLength, MinimumLength = Limits.VacancyTextMinLength)]
    public string Text { get; set => field = value?.ReplaceLineEndings("\n").Trim() ?? ""; } = "";
}

public record VacancyResponse(Guid Id, string Title, string Text, DateTimeOffset CreatedAt)
{
    public static VacancyResponse From(Vacancy vacancy) =>
        new(vacancy.Id, vacancy.Title, vacancy.Text, vacancy.CreatedAt);
}

public static class VacancyEndpoints
{
    public static IEndpointRouteBuilder MapVacancyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vacancies").WithTags("Vacancies");

        group.MapPost("/", Create);
        group.MapGet("/{id:guid}", Get).WithName("GetVacancy");

        return app;
    }

    // The request is validated against its data annotations before this runs (AddValidation).
    private static async Task<CreatedAtRoute<VacancyResponse>> Create(
        CreateVacancyRequest request, AppDbContext db, CancellationToken ct)
    {
        var vacancy = new Vacancy
        {
            Id = Guid.CreateVersion7(),
            Title = request.Title,
            Text = request.Text,
            CreatedAt = Timestamp.Now(),
        };
        db.Vacancies.Add(vacancy);
        await db.SaveChangesAsync(ct);

        return TypedResults.CreatedAtRoute(VacancyResponse.From(vacancy), "GetVacancy", new { id = vacancy.Id });
    }

    private static async Task<Results<Ok<VacancyResponse>, NotFound>> Get(Guid id, AppDbContext db, CancellationToken ct)
    {
        var vacancy = await db.Vacancies.FindAsync([id], ct);
        return vacancy is null ? TypedResults.NotFound() : TypedResults.Ok(VacancyResponse.From(vacancy));
    }
}
