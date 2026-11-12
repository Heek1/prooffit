using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using ProofFit.Api.Features.Resumes;

namespace ProofFit.Api.Tests;

[Collection(ApiCollection.Name)]
public class ResumeEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Upload_stores_the_extracted_text_and_returns_it()
    {
        var pdf = TestPdf.WithLines("Vlad, full-stack developer", "ASP.NET Core, React, Docker");

        var response = await Upload(pdf, "resume.pdf");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ResumeResponse>();
        Assert.NotNull(created);
        Assert.Equal("resume.pdf", created.FileName);
        Assert.Equal(1, created.PageCount);
        Assert.Equal("Vlad, full-stack developer\nASP.NET Core, React, Docker", created.Text);

        // The Location header points at the stored resume.
        var fetched = await _client.GetFromJsonAsync<ResumeResponse>(response.Headers.Location);
        Assert.Equal(created, fetched);
    }

    [Fact]
    public async Task Upload_rejects_a_file_that_is_not_a_pdf()
    {
        var response = await Upload(Encoding.UTF8.GetBytes("just some text"), "resume.pdf");

        await AssertFileError(response, "The file is not a readable PDF.");
    }

    [Fact]
    public async Task Upload_rejects_a_pdf_without_selectable_text()
    {
        var response = await Upload(TestPdf.Blank(), "scan.pdf");

        await AssertFileError(response, "The PDF has no selectable text. A scanned resume has to be exported as text first.");
    }

    [Fact]
    public async Task Upload_rejects_an_empty_file()
    {
        var response = await Upload([], "empty.pdf");

        await AssertFileError(response, "The file is empty.");
    }

    [Fact]
    public async Task Upload_keeps_only_the_file_name_from_a_client_path()
    {
        var response = await Upload(TestPdf.WithLines("Resume"), "../../etc/resume.pdf");

        var created = await response.Content.ReadFromJsonAsync<ResumeResponse>();
        Assert.Equal("resume.pdf", created?.FileName);
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_id()
    {
        var response = await _client.GetAsync($"/api/resumes/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<HttpResponseMessage> Upload(byte[] content, string fileName)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return _client.PostAsync("/api/resumes", new MultipartFormDataContent { { file, "file", fileName } });
    }

    private static async Task AssertFileError(HttpResponseMessage response, string expected)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal([expected], problem.Errors["file"]);
    }
}
