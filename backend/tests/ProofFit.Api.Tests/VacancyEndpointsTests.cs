using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Vacancies;

namespace ProofFit.Api.Tests;

[Collection(ApiCollection.Name)]
public class VacancyEndpointsTests(ApiFactory factory)
{
    private const string ValidText =
        "We are looking for a full-stack developer with ASP.NET Core and React experience.";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_stores_the_vacancy_and_returns_it()
    {
        var response = await _client.PostAsJsonAsync("/api/vacancies",
            new { title = "  Full-stack developer ", text = ValidText });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<VacancyResponse>();
        Assert.NotNull(created);
        Assert.Equal("Full-stack developer", created.Title);
        Assert.Equal(ValidText, created.Text);

        var fetched = await _client.GetFromJsonAsync<VacancyResponse>(response.Headers.Location);
        Assert.Equal(created, fetched);
    }

    [Fact]
    public async Task Create_rejects_a_text_too_short_to_analyse()
    {
        var response = await _client.PostAsJsonAsync("/api/vacancies",
            new { title = "Developer", text = "Too short." });

        var problem = await AssertValidationProblem(response);
        Assert.Contains("Text", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_does_not_count_surrounding_whitespace_towards_the_minimum()
    {
        var response = await _client.PostAsJsonAsync("/api/vacancies",
            new { title = "Developer", text = "Too short." + new string(' ', Limits.VacancyTextMinLength) });

        var problem = await AssertValidationProblem(response);
        Assert.Contains("Text", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_rejects_a_text_over_the_limit()
    {
        var response = await _client.PostAsJsonAsync("/api/vacancies",
            new { title = "Developer", text = new string('a', Limits.VacancyTextMaxLength + 1) });

        var problem = await AssertValidationProblem(response);
        Assert.Contains("Text", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_rejects_a_missing_title()
    {
        var response = await _client.PostAsJsonAsync("/api/vacancies", new { text = ValidText });

        var problem = await AssertValidationProblem(response);
        Assert.Contains("Title", problem.Errors.Keys);
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_id()
    {
        var response = await _client.GetAsync($"/api/vacancies/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<ValidationProblemDetails> AssertValidationProblem(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        return problem;
    }
}
