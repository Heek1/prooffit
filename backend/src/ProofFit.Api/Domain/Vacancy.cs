namespace ProofFit.Api.Domain;

public class Vacancy
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Text { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
