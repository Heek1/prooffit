using System.Text.RegularExpressions;

namespace ProofFit.Api.Features.Analyses.Matching;

/// <summary>A named technology or skill and the spellings it appears under.</summary>
public sealed class Skill
{
    private readonly Regex _pattern;

    /// <param name="name">How the skill is shown in reports.</param>
    /// <param name="aliases">
    /// Spellings to look for. An alias written in capitals ("REST", "AWS") is matched
    /// case-sensitively, because its lowercase form is an ordinary word or too short to trust.
    /// </param>
    public Skill(string name, params string[] aliases)
    {
        Name = name;

        // A match must not sit inside a longer word, so "Java" is not found in "JavaScript"
        // and ".NET" is not found in "ASP.NET". '+' and '#' count as word characters to
        // keep "C" out of "C++" and "C#".
        var alternatives = aliases.Select(alias =>
            (IsAllCaps(alias) ? "(?-i:" : "(?:") + Regex.Escape(alias) + ")");
        _pattern = new Regex(
            @"(?<![\p{L}\p{N}])(?:" + string.Join('|', alternatives) + @")(?![\p{L}\p{N}+#])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    public string Name { get; }

    public bool IsIn(string text) => _pattern.IsMatch(text);

    private static bool IsAllCaps(string alias) =>
        alias.Any(char.IsLetter) && !alias.Any(char.IsLower);
}

/// <summary>
/// The technologies the keyword baseline can recognise. It is deliberately a plain list:
/// the baseline exists to be simple and predictable, and to give the model-based matcher
/// something to be measured against.
/// </summary>
public static class SkillCatalog
{
    public static readonly IReadOnlyList<Skill> All =
    [
        // Languages
        new("C#", "c#", "csharp"),
        new(".NET", ".net", "dotnet"),
        new("JavaScript", "javascript", "JS", "ES6"),
        new("TypeScript", "typescript"),
        new("Python", "python"),
        new("Java", "java"),
        new("Go", "golang"),
        new("C++", "c++"),
        new("PHP", "php"),
        new("SQL", "SQL"),
        new("HTML", "html", "html5"),
        new("CSS", "css", "css3", "sass", "scss"),

        // Frontend
        new("React", "react", "react.js", "reactjs"),
        new("Angular", "angular"),
        new("Vue", "vue", "vue.js", "vuejs"),
        new("Next.js", "next.js", "nextjs"),
        new("Redux", "redux"),
        new("Tailwind CSS", "tailwind", "tailwindcss"),

        // Backend
        new("ASP.NET Core", "asp.net core", "asp.net", "aspnet"),
        new("Entity Framework Core", "entity framework core", "entity framework", "ef core"),
        new("Node.js", "node.js", "nodejs", "node"),
        new("Express", "express.js", "expressjs"),
        new("NestJS", "nestjs", "nest.js"),
        new("FastAPI", "fastapi"),
        new("Django", "django"),
        new("Flask", "flask"),
        new("Spring", "spring boot", "spring"),
        new("SignalR", "signalr"),
        new("REST APIs", "REST", "restful", "rest api", "rest apis"),
        new("GraphQL", "graphql"),
        new("gRPC", "grpc"),
        new("WebSockets", "websocket", "websockets"),
        new("Microservices", "microservice", "microservices"),
        new("Authentication", "oauth", "oauth2", "JWT", "openid connect"),

        // Data
        new("PostgreSQL", "postgresql", "postgres"),
        new("MySQL", "mysql"),
        new("SQL Server", "sql server", "mssql", "ms sql"),
        new("SQLite", "sqlite"),
        new("MongoDB", "mongodb", "mongo"),
        new("Redis", "redis"),
        new("Elasticsearch", "elasticsearch"),
        new("Kafka", "kafka"),
        new("RabbitMQ", "rabbitmq"),
        new("Vector search", "pgvector", "vector database", "vector search", "embeddings"),

        // Delivery
        new("Docker", "docker", "docker compose"),
        new("Kubernetes", "kubernetes", "k8s"),
        new("CI/CD", "ci/cd", "ci / cd", "continuous integration", "continuous delivery",
            "github actions", "gitlab ci", "jenkins", "azure pipelines"),
        new("Git", "git"),
        new("Azure", "azure"),
        new("AWS", "AWS", "amazon web services"),
        new("Google Cloud", "GCP", "google cloud"),
        new("Linux", "linux"),
        new("Terraform", "terraform"),

        // Quality
        new("Automated testing", "unit test", "unit tests", "unit testing", "integration tests",
            "automated tests", "automated testing", "xunit", "nunit", "jest", "vitest", "pytest", "TDD"),

        // AI
        new("LLM APIs", "LLM", "LLMs", "large language model", "large language models", "openai",
            "gemini", "claude", "anthropic", "GPT", "generative ai", "genai"),
        new("RAG", "RAG", "retrieval-augmented generation", "retrieval augmented generation"),
        new("Machine learning", "machine learning", "ML"),

        // Ways of working
        new("Agile", "agile", "scrum", "kanban"),
        new("English", "english"),
    ];

    private static readonly Dictionary<string, Skill> ByName =
        All.ToDictionary(skill => skill.Name, StringComparer.Ordinal);

    /// <summary>The skills mentioned in a piece of text, in catalog order.</summary>
    public static IReadOnlyList<Skill> FindIn(string text) => All.Where(skill => skill.IsIn(text)).ToList();

    /// <summary>Looks a skill up by its exact catalog name.</summary>
    public static Skill Get(string name) => ByName[name];
}
