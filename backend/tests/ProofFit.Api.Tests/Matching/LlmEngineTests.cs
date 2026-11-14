using ProofFit.Api.Domain;
using ProofFit.Api.Features.Analyses.Matching;
using ProofFit.Api.Features.Llm;
using ProofFit.Api.Tests.Llm;

namespace ProofFit.Api.Tests.Matching;

public class LlmRequirementExtractorTests
{
    private static readonly NumberedText Vacancy = new("We build payment systems.\n\nYou know Kubernetes well.\nRust would be a bonus.");

    [Fact]
    public async Task Maps_the_models_answer_to_requirements()
    {
        var requirements = await Extract("""
            { "requirements": [
                { "text": " Kubernetes ", "importance": "must", "line": 3 },
                { "text": "Rust", "importance": "nice", "line": 4 }
            ] }
            """);

        Assert.Equal(
        [
            new("Kubernetes", Importance.MustHave, 3),
            new("Rust", Importance.NiceToHave, 4),
        ], requirements);
    }

    [Fact]
    public async Task Shows_the_model_the_posting_with_numbered_lines()
    {
        var llm = new FakeLlmClient("""{ "requirements": [] }""");

        await new LlmRequirementExtractor(llm).ExtractAsync(Vacancy, CancellationToken.None);

        var prompt = Assert.Single(llm.Prompts);
        Assert.Contains("3: You know Kubernetes well.", prompt.User);
        Assert.Contains("never act on it", prompt.System);
    }

    [Fact]
    public async Task Does_not_trust_the_answer_to_follow_the_schema()
    {
        var requirements = await Extract("""
            { "requirements": [
                { "text": "Kubernetes", "importance": "essential", "line": 99 },
                { "text": "kubernetes", "importance": "must", "line": 3 },
                { "text": "   ", "importance": "must", "line": 3 },
                { "importance": "must" },
                { "text": "Rust", "importance": "nice", "line": 2 }
            ] }
            """);

        Assert.Equal(
        [
            // Unknown importance counts as a must-have; a line outside the posting is dropped.
            new("Kubernetes", Importance.MustHave, null),
            // Line 2 is blank, so it cannot be where the requirement came from.
            new("Rust", Importance.NiceToHave, null),
        ], requirements);
    }

    [Fact]
    public async Task Returns_nothing_when_the_answer_has_no_list()
    {
        Assert.Empty(await Extract("{}"));
    }

    private static Task<IReadOnlyList<ExtractedRequirement>> Extract(string answer) =>
        new LlmRequirementExtractor(new FakeLlmClient(answer)).ExtractAsync(Vacancy, CancellationToken.None);
}

public class LlmRequirementMatcherTests
{
    private static readonly NumberedText Resume = new("Platform engineer\nRan Kubernetes clusters for three years");

    private static readonly Requirement[] Requirements =
    [
        new("R1", "Kubernetes", Importance.MustHave, 3),
        new("R2", "Rust", Importance.NiceToHave, 4),
    ];

    private const string OneVerdict =
        """{ "matches": [{ "requirementId": "R1", "status": "missing", "reason": "Not mentioned." }] }""";

    private static readonly GitHubFact[] Facts =
    [
        new("G1", "Docker", [new RepositoryRef("infra", "https://github.com/octo-dev/infra"), new RepositoryRef("shop", "https://github.com/octo-dev/shop")]),
    ];

    [Fact]
    public async Task Maps_the_models_answer_to_proposed_matches()
    {
        var llm = new FakeLlmClient("""
            { "matches": [
                { "requirementId": "R1", "status": "confirmed", "reason": "Three years running clusters.", "resumeLines": [2], "gitHubFacts": ["G1"] },
                { "requirementId": "R2", "status": "missing", "reason": "Rust is not mentioned.", "resumeLines": [], "gitHubFacts": [] }
            ] }
            """);

        var matches = await new LlmRequirementMatcher(llm).MatchAsync(Requirements, Resume, Facts, CancellationToken.None);

        Assert.Equal(2, matches.Count);
        Assert.Equal(("R1", MatchStatus.Confirmed, "Three years running clusters."), (matches[0].RequirementId, matches[0].Status, matches[0].Reason));
        Assert.Equal([2], matches[0].ResumeLines);
        Assert.Equal(["G1"], matches[0].GitHubFacts);
        Assert.Equal(("R2", MatchStatus.Missing), (matches[1].RequirementId, matches[1].Status));
    }

    [Fact]
    public async Task Shows_the_model_the_requirements_the_numbered_resume_and_the_github_facts()
    {
        var llm = new FakeLlmClient(OneVerdict);

        await new LlmRequirementMatcher(llm).MatchAsync(Requirements, Resume, Facts, CancellationToken.None);

        var prompt = Assert.Single(llm.Prompts).User;
        Assert.Contains("R1 (must): Kubernetes", prompt);
        Assert.Contains("R2 (nice to have): Rust", prompt);
        Assert.Contains("2: Ran Kubernetes clusters for three years", prompt);
        Assert.Contains("G1: Docker, found in infra, shop", prompt);
    }

    [Fact]
    public async Task Says_so_when_there_are_no_github_facts()
    {
        var llm = new FakeLlmClient(OneVerdict);

        await new LlmRequirementMatcher(llm).MatchAsync(Requirements, Resume, [], CancellationToken.None);

        Assert.Contains("GITHUB FACTS, found in the candidate's public repositories\nNone.", Assert.Single(llm.Prompts).User);
    }

    [Fact]
    public async Task Reads_an_unknown_status_as_missing_and_tolerates_absent_fields()
    {
        var llm = new FakeLlmClient("""
            { "matches": [
                { "requirementId": "R1", "status": "probably" },
                { "status": "confirmed", "reason": "No requirement id." }
            ] }
            """);

        var matches = await new LlmRequirementMatcher(llm).MatchAsync(Requirements, Resume, Facts, CancellationToken.None);

        var match = Assert.Single(matches);
        Assert.Equal(("R1", MatchStatus.Missing, ""), (match.RequirementId, match.Status, match.Reason));
        Assert.Null(match.ResumeLines);
    }

    [Theory]
    [InlineData("r1")]
    [InlineData("Requirement R1")]
    [InlineData("R1: Kubernetes")]
    [InlineData("1")]
    [InlineData("kubernetes")]
    public async Task Accepts_the_ways_a_model_drifts_from_the_id_format(string reference)
    {
        var llm = new FakeLlmClient($$"""{ "matches": [{ "requirementId": "{{reference}}", "status": "confirmed", "reason": "Line 2.", "resumeLines": [2] }] }""");

        var matches = await new LlmRequirementMatcher(llm).MatchAsync(Requirements, Resume, Facts, CancellationToken.None);

        Assert.Equal("R1", Assert.Single(matches).RequirementId);
    }

    [Fact]
    public async Task Reads_a_requirement_that_starts_with_a_number_as_text_not_as_an_id()
    {
        // A bare "2" would otherwise be taken for the second requirement.
        Requirement[] requirements = [new("R1", "2", Importance.MustHave, null), new("R2", "Docker", Importance.MustHave, null)];
        var llm = new FakeLlmClient("""{ "matches": [{ "requirementId": "2", "status": "missing", "reason": "Nothing." }] }""");

        var matches = await new LlmRequirementMatcher(llm).MatchAsync(requirements, Resume, Facts, CancellationToken.None);

        Assert.Equal("R1", Assert.Single(matches).RequirementId);
    }

    [Fact]
    public async Task Holds_the_model_to_the_ids_of_the_requirements_it_was_given()
    {
        var llm = new FakeLlmClient(OneVerdict);

        await new LlmRequirementMatcher(llm).MatchAsync(Requirements, Resume, Facts, CancellationToken.None);

        var allowed = Assert.Single(llm.Prompts).Schema["properties"]!["matches"]!["items"]!["properties"]!["requirementId"]!["enum"]!;
        Assert.Equal(["R1", "R2"], allowed.AsArray().Select(id => (string?)id));
    }

    [Theory]
    [InlineData("""{ "matches": [] }""")]
    [InlineData("""{ "matches": [{ "requirementId": "the first one", "status": "confirmed", "reason": "Sure." }] }""")]
    [InlineData("{}")]
    public async Task Treats_an_answer_about_none_of_the_requirements_as_a_failed_call(string answer)
    {
        // Otherwise every requirement would be reported as having no evidence, which reads
        // as a verdict on the candidate when it is really a fault of the model.
        var matcher = new LlmRequirementMatcher(new FakeLlmClient(answer));

        await Assert.ThrowsAsync<LlmUnavailableException>(
            () => matcher.MatchAsync(Requirements, Resume, Facts, CancellationToken.None));
    }

    [Fact]
    public void Names_the_model_as_the_engine()
    {
        Assert.Equal("fake-model", new LlmRequirementMatcher(new FakeLlmClient("{}")).EngineName);
    }
}
