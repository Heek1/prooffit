using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace ProofFit.Api.Features.Resumes;

/// <param name="IgnoredCharacters">Characters left out because no reader could see them.</param>
public record PdfText(string Text, int PageCount, int IgnoredCharacters);

public class PdfReadException(string message, Exception inner) : Exception(message, inner);

public class PdfTextExtractor
{
    // Below this size text cannot be read on a printed page or a screen at normal zoom.
    private const double MinReadablePointSize = 3;

    // White, or close enough to it that it vanishes on a white page.
    private const double NearWhite = 0.95;

    /// <summary>
    /// Reads the selectable text of a PDF, pages separated by a blank line, leaving out
    /// text a person reading the resume cannot see.
    /// </summary>
    /// <remarks>
    /// Hidden text is the oldest trick against automated screening: a vacancy's keywords in
    /// white on white, or in a one-point font. A reader never sees it, so it is not evidence
    /// of anything, and counting it would let anyone write their own verdicts.
    /// </remarks>
    /// <exception cref="PdfReadException">The stream is not a PDF this library can open.</exception>
    public PdfText Extract(Stream pdf)
    {
        try
        {
            using var document = PdfDocument.Open(pdf);

            var ignored = 0;
            var pages = new List<string>();
            foreach (var page in document.GetPages())
            {
                var hidden = page.Letters.Count(letter => IsInvisible(letter) && !string.IsNullOrWhiteSpace(letter.Value));
                ignored += hidden;

                // ContentOrderTextExtractor keeps words and lines apart; Page.Text glues every
                // letter together, which would destroy the quotes the report later cites. It
                // works on the whole page, so a page with hidden text is rebuilt from its
                // visible letters instead.
                var text = hidden == 0 ? ContentOrderTextExtractor.GetText(page) : VisibleText(page);
                text = text.Trim();
                if (text.Length > 0) pages.Add(text);
            }

            var joined = string.Join("\n\n", pages).ReplaceLineEndings("\n");
            return new PdfText(joined, document.NumberOfPages, ignored);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // PdfPig reports a damaged, encrypted or non-PDF file through several unrelated
            // exception types; to the caller they all mean the same thing.
            throw new PdfReadException("The file is not a readable PDF.", ex);
        }
    }

    private static bool IsInvisible(Letter letter)
    {
        if (letter.RenderingMode is TextRenderingMode.Neither or TextRenderingMode.NeitherClip) return true;
        if (letter.PointSize < MinReadablePointSize) return true;

        var (red, green, blue) = letter.Color?.ToRGBValues() ?? (0, 0, 0);
        return red >= NearWhite && green >= NearWhite && blue >= NearWhite;
    }

    // Visible letters grouped into words, words into lines by where they sit on the page,
    // lines read top to bottom and words left to right.
    private static string VisibleText(Page page)
    {
        var letters = page.Letters.Where(letter => !IsInvisible(letter)).ToList();
        var words = NearestNeighbourWordExtractor.Instance.GetWords(letters)
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .OrderByDescending(word => word.BoundingBox.Bottom)
            .ToList();

        var lines = new List<List<Word>>();
        foreach (var word in words)
        {
            var line = lines.LastOrDefault();
            var sameLine = line is not null &&
                Math.Abs(line[0].BoundingBox.Bottom - word.BoundingBox.Bottom) < Math.Max(line[0].BoundingBox.Height, 1) / 2;
            if (sameLine) line!.Add(word);
            else lines.Add([word]);
        }

        return string.Join('\n', lines.Select(line =>
            string.Join(' ', line.OrderBy(word => word.BoundingBox.Left).Select(word => word.Text))));
    }
}
