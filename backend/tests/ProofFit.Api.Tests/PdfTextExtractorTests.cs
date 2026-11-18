using System.Text;
using ProofFit.Api.Domain;
using ProofFit.Api.Features.Resumes;

namespace ProofFit.Api.Tests;

public class PdfTextExtractorTests
{
    private readonly PdfTextExtractor _extractor = new();

    [Fact]
    public void Keeps_words_and_lines_separate()
    {
        var pdf = TestPdf.WithLines("Senior .NET developer", "Built a billing API with ASP.NET Core");

        var result = _extractor.Extract(new MemoryStream(pdf));

        Assert.Equal("Senior .NET developer\nBuilt a billing API with ASP.NET Core", result.Text);
        Assert.Equal(1, result.PageCount);
    }

    [Fact]
    public void Separates_pages_with_a_blank_line()
    {
        var pdf = TestPdf.WithPages(["Experience"], ["Education"]);

        var result = _extractor.Extract(new MemoryStream(pdf));

        Assert.Equal("Experience\n\nEducation", result.Text);
        Assert.Equal(2, result.PageCount);
    }

    [Fact]
    public void Returns_empty_text_for_a_pdf_without_text()
    {
        var result = _extractor.Extract(new MemoryStream(TestPdf.Blank()));

        Assert.Equal("", result.Text);
        Assert.Equal(1, result.PageCount);
    }

    [Fact]
    public void Rejects_more_pages_than_a_resume_has()
    {
        var pages = Enumerable.Range(1, Limits.ResumePages + 1).Select(n => new[] { $"Page {n}" }).ToArray();

        var error = Assert.Throws<PdfReadException>(() => _extractor.Extract(new MemoryStream(TestPdf.WithPages(pages))));

        Assert.Contains($"at most {Limits.ResumePages}", error.Message);
    }

    [Fact]
    public void Reads_a_resume_at_the_page_limit()
    {
        var pages = Enumerable.Range(1, Limits.ResumePages).Select(n => new[] { $"Page {n}" }).ToArray();

        Assert.Equal(Limits.ResumePages, _extractor.Extract(new MemoryStream(TestPdf.WithPages(pages))).PageCount);
    }

    [Fact]
    public void Rejects_more_text_than_a_resume_holds()
    {
        // Lines run past the page edge, but the text is still in the file; three pages of
        // 35 lines of 400 characters is 42 000 characters.
        var line = new string('x', 400);
        var pages = Enumerable.Range(1, 3).Select(_ => Enumerable.Repeat(line, 35).ToArray()).ToArray();

        var error = Assert.Throws<PdfReadException>(() => _extractor.Extract(new MemoryStream(TestPdf.WithPages(pages))));

        Assert.Contains("too long for a resume", error.Message);
    }

    [Fact]
    public void Stops_reading_a_pdf_that_takes_too_long()
    {
        var slow = new PdfTextExtractor(readTime: TimeSpan.Zero);

        var error = Assert.Throws<PdfReadException>(() => slow.Extract(new MemoryStream(TestPdf.WithLines("Experience"))));

        Assert.Contains("took too long", error.Message);
    }

    [Fact]
    public void Rejects_a_file_that_is_not_a_pdf()
    {
        var notPdf = Encoding.UTF8.GetBytes("This is a plain text file, not a PDF.");

        Assert.Throws<PdfReadException>(() => _extractor.Extract(new MemoryStream(notPdf)));
    }
}
