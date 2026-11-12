using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ProofFit.Api.Tests;

/// <summary>Builds small PDFs in memory so tests need no fixture files.</summary>
public static class TestPdf
{
    /// <summary>One page per argument; each page holds its lines top to bottom.</summary>
    public static byte[] WithPages(params string[][] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        foreach (var lines in pages)
        {
            var page = builder.AddPage(PageSize.A4);
            var y = 780.0;
            foreach (var line in lines)
            {
                page.AddText(line, 12, new PdfPoint(40, y), font);
                y -= 20;
            }
        }

        return builder.Build();
    }

    public static byte[] WithLines(params string[] lines) => WithPages(lines);

    /// <summary>A valid one-page PDF with no text, like a scanned document.</summary>
    public static byte[] Blank() => WithPages([[]]);
}
