using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ProofFit.Api.Features.Resumes;

public record PdfText(string Text, int PageCount);

public class PdfReadException(string message, Exception inner) : Exception(message, inner);

public class PdfTextExtractor
{
    /// <summary>Reads the selectable text of a PDF, pages separated by a blank line.</summary>
    /// <exception cref="PdfReadException">The stream is not a PDF this library can open.</exception>
    public PdfText Extract(Stream pdf)
    {
        try
        {
            using var document = PdfDocument.Open(pdf);

            // ContentOrderTextExtractor keeps words and lines apart; Page.Text glues every
            // letter together, which would destroy the quotes the report later cites.
            var pages = document.GetPages()
                .Select(page => ContentOrderTextExtractor.GetText(page).Trim())
                .Where(text => text.Length > 0);

            var text = string.Join("\n\n", pages).ReplaceLineEndings("\n");
            return new PdfText(text, document.NumberOfPages);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // PdfPig reports a damaged, encrypted or non-PDF file through several unrelated
            // exception types; to the caller they all mean the same thing.
            throw new PdfReadException("The file is not a readable PDF.", ex);
        }
    }
}
