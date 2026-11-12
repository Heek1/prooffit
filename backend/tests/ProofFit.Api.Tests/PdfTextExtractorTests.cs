using System.Text;
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
    public void Rejects_a_file_that_is_not_a_pdf()
    {
        var notPdf = Encoding.UTF8.GetBytes("This is a plain text file, not a PDF.");

        Assert.Throws<PdfReadException>(() => _extractor.Extract(new MemoryStream(notPdf)));
    }
}
