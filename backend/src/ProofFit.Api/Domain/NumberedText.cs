namespace ProofFit.Api.Domain;

/// <summary>
/// A document as numbered lines. Line numbers are 1-based and are how evidence is cited, so
/// the split here must stay identical to the one the frontend uses to display the text.
/// </summary>
public sealed class NumberedText(string text)
{
    public IReadOnlyList<string> Lines { get; } = text.Split('\n');

    /// <summary>The text of a line. Throws for a number outside the document.</summary>
    public string this[int line] => Lines[line - 1];

    /// <summary>True when the line exists and is not blank, so it can serve as evidence.</summary>
    public bool HasContent(int line) =>
        line >= 1 && line <= Lines.Count && !string.IsNullOrWhiteSpace(Lines[line - 1]);

    /// <summary>The document with each line prefixed by its number, for a model to cite.</summary>
    public string ToPrompt() => string.Join('\n', Lines.Select((line, index) => $"{index + 1}: {line}"));
}
