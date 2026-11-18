namespace ProofFit.Api.Domain;

public class Resume
{
    public Guid Id { get; set; }
    public required string FileName { get; set; }
    public required string Text { get; set; }
    public int PageCount { get; set; }

    /// <summary>Characters in the PDF left out because a reader could not see them.</summary>
    public int IgnoredCharacters { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}
