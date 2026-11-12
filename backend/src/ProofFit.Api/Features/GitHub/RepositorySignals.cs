using System.Text.Json;
using System.Text.RegularExpressions;

namespace ProofFit.Api.Features.GitHub;

/// <summary>
/// Works out which skills a repository demonstrates from its file paths and dependency
/// manifests. Everything here is a fixed rule over files anyone can open, so a fact in a
/// report can always be traced to something concrete in the repository.
/// Skill names are those of the skill catalog.
/// </summary>
public static partial class RepositorySignals
{
    private const int MaxManifests = 8;
    private const int MaxManifestDepth = 3;

    // A language counts when it has a few files or makes up a real share of the code, so
    // one stray script or a template's bundled site.js does not claim a language.
    private const int LanguageFileCount = 3;
    private const double LanguageShare = 0.3;

    private static readonly Dictionary<string, string> LanguageByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "C#", [".ts"] = "TypeScript", [".tsx"] = "TypeScript",
        [".js"] = "JavaScript", [".jsx"] = "JavaScript", [".mjs"] = "JavaScript",
        [".py"] = "Python", [".java"] = "Java", [".go"] = "Go", [".php"] = "PHP",
        [".cpp"] = "C++", [".cc"] = "C++", [".cxx"] = "C++", [".hpp"] = "C++",
        [".html"] = "HTML", [".css"] = "CSS", [".scss"] = "CSS", [".sql"] = "SQL",
    };

    private static readonly (Regex Path, string Skill)[] PathRules =
    [
        (Rx(@"(^|/)Dockerfile[^/]*$|(^|/)(docker-)?compose[^/]*\.ya?ml$"), "Docker"),
        (Rx(@"^\.github/workflows/[^/]+\.ya?ml$|(^|/)\.gitlab-ci\.yml$|(^|/)Jenkinsfile$|(^|/)azure-pipelines\.yml$"), "CI/CD"),
        (Rx(@"(^|/)(tests?|__tests__|specs?)/|\.Tests?/|[._-](test|spec)\.[a-z]+$|(^|/)test_[^/]+\.py$|Tests?\.cs$"), "Automated testing"),
        (Rx(@"(^|/)(k8s|kubernetes|helm)/|(^|/)Chart\.yaml$"), "Kubernetes"),
        (Rx(@"\.tf$"), "Terraform"),
        (Rx(@"(^|/)angular\.json$"), "Angular"),
        (Rx(@"\.vue$"), "Vue"),
        (Rx(@"(^|/)next\.config\.[a-z]+$"), "Next.js"),
        (Rx(@"(^|/)tailwind\.config\.[a-z]+$"), "Tailwind CSS"),
        (Rx(@"\.(tsx|jsx)$"), "React"),
        (Rx(@"\.(csproj|sln|slnx)$"), ".NET"),
        (Rx(@"\.(cshtml|razor)$"), "ASP.NET Core"),
        (Rx(@"\.proto$"), "gRPC"),
        (Rx(@"\.(graphql|gql)$"), "GraphQL"),
    ];

    // npm package name, exact.
    private static readonly ILookup<string, string> NpmPackages = Lookup(
        ("react", "React"), ("react-dom", "React"), ("next", "Next.js"), ("@angular/core", "Angular"),
        ("vue", "Vue"), ("redux", "Redux"), ("@reduxjs/toolkit", "Redux"), ("tailwindcss", "Tailwind CSS"),
        ("typescript", "TypeScript"),
        ("express", "Express"), ("express", "Node.js"), ("@nestjs/core", "NestJS"), ("@nestjs/core", "Node.js"),
        ("fastify", "Node.js"), ("koa", "Node.js"),
        ("graphql", "GraphQL"), ("@apollo/client", "GraphQL"), ("@apollo/server", "GraphQL"),
        ("socket.io", "WebSockets"), ("ws", "WebSockets"), ("@grpc/grpc-js", "gRPC"),
        ("pg", "PostgreSQL"), ("mysql2", "MySQL"), ("mongoose", "MongoDB"), ("mongodb", "MongoDB"),
        ("redis", "Redis"), ("ioredis", "Redis"), ("kafkajs", "Kafka"), ("amqplib", "RabbitMQ"),
        ("jest", "Automated testing"), ("vitest", "Automated testing"), ("mocha", "Automated testing"),
        ("@playwright/test", "Automated testing"), ("cypress", "Automated testing"),
        ("openai", "LLM APIs"), ("@anthropic-ai/sdk", "LLM APIs"), ("@google/generative-ai", "LLM APIs"),
        ("@google/genai", "LLM APIs"), ("langchain", "LLM APIs"),
        ("jsonwebtoken", "Authentication"), ("passport", "Authentication"), ("next-auth", "Authentication"));

    // NuGet package id prefix, case-insensitive.
    private static readonly (string Prefix, string Skill)[] NuGetPackages =
    [
        ("Microsoft.AspNetCore", "ASP.NET Core"), ("Microsoft.EntityFrameworkCore", "Entity Framework Core"),
        ("Npgsql.EntityFrameworkCore", "Entity Framework Core"),
        ("Npgsql", "PostgreSQL"), ("Microsoft.EntityFrameworkCore.SqlServer", "SQL Server"),
        ("Microsoft.Data.SqlClient", "SQL Server"), ("Microsoft.EntityFrameworkCore.Sqlite", "SQLite"),
        ("Microsoft.Data.Sqlite", "SQLite"), ("MongoDB.Driver", "MongoDB"), ("StackExchange.Redis", "Redis"),
        ("Confluent.Kafka", "Kafka"), ("RabbitMQ.Client", "RabbitMQ"),
        ("xunit", "Automated testing"), ("NUnit", "Automated testing"), ("MSTest", "Automated testing"),
        ("Grpc.", "gRPC"), ("HotChocolate", "GraphQL"), ("Microsoft.AspNetCore.SignalR", "SignalR"),
        ("Microsoft.AspNetCore.Authentication", "Authentication"),
        ("Microsoft.Extensions.AI", "LLM APIs"), ("Microsoft.SemanticKernel", "LLM APIs"), ("OpenAI", "LLM APIs"),
        ("Anthropic", "LLM APIs"), ("Google.GenAI", "LLM APIs"), ("Pgvector", "Vector search"),
    ];

    // Python distribution name, exact and lowercase.
    private static readonly ILookup<string, string> PythonPackages = Lookup(
        ("fastapi", "FastAPI"), ("django", "Django"), ("flask", "Flask"), ("pytest", "Automated testing"),
        ("psycopg", "PostgreSQL"), ("psycopg2", "PostgreSQL"), ("psycopg2-binary", "PostgreSQL"),
        ("asyncpg", "PostgreSQL"), ("pymongo", "MongoDB"), ("motor", "MongoDB"), ("redis", "Redis"),
        ("kafka-python", "Kafka"), ("pika", "RabbitMQ"), ("grpcio", "gRPC"), ("websockets", "WebSockets"),
        ("openai", "LLM APIs"), ("anthropic", "LLM APIs"), ("google-generativeai", "LLM APIs"),
        ("google-genai", "LLM APIs"), ("langchain", "LLM APIs"), ("llama-index", "LLM APIs"),
        ("pgvector", "Vector search"), ("chromadb", "Vector search"), ("faiss-cpu", "Vector search"),
        ("qdrant-client", "Vector search"),
        ("scikit-learn", "Machine learning"), ("torch", "Machine learning"), ("tensorflow", "Machine learning"),
        ("pyjwt", "Authentication"), ("python-jose", "Authentication"));

    /// <summary>Every skill name the rules can produce, so a test can hold them to the catalog.</summary>
    public static IEnumerable<string> SkillNames() => LanguageByExtension.Values
        .Concat(PathRules.Select(rule => rule.Skill))
        .Concat(NpmPackages.SelectMany(group => group))
        .Concat(NuGetPackages.Select(package => package.Skill))
        .Concat(PythonPackages.SelectMany(group => group))
        .Append("RAG")
        .Distinct();

    /// <summary>The dependency manifests worth downloading: those nearest the root, a bounded number.</summary>
    public static IReadOnlyList<string> ManifestsToRead(IReadOnlyList<string> paths) => paths
        .Where(path => !IsVendored(path) && Depth(path) <= MaxManifestDepth && ManifestKind(path) is not null)
        .OrderBy(Depth)
        .ThenBy(path => path, StringComparer.Ordinal)
        .Take(MaxManifests)
        .ToList();

    /// <param name="paths">Every file path in the repository.</param>
    /// <param name="manifests">Contents of the manifests that could be read, by path.</param>
    public static IReadOnlySet<string> Detect(IReadOnlyList<string> paths, IReadOnlyDictionary<string, string> manifests)
    {
        var skills = new HashSet<string>(StringComparer.Ordinal);
        var source = paths.Where(path => !IsVendored(path)).ToList();

        var byLanguage = source
            .Select(path => LanguageByExtension.GetValueOrDefault(Path.GetExtension(path)))
            .Where(language => language is not null)
            .GroupBy(language => language!)
            .ToDictionary(group => group.Key, group => group.Count());
        var codeFiles = byLanguage.Values.Sum();
        foreach (var (language, count) in byLanguage)
        {
            if (count >= LanguageFileCount || count >= codeFiles * LanguageShare) skills.Add(language);
        }

        foreach (var (pattern, skill) in PathRules)
        {
            if (source.Any(pattern.IsMatch)) skills.Add(skill);
        }

        foreach (var (path, content) in manifests)
        {
            skills.UnionWith(ManifestKind(path) switch
            {
                "npm" => FromPackageJson(content),
                "nuget" => FromProjectFile(content),
                "python" => FromPythonManifest(content),
                _ => [],
            });
        }

        // A language-model SDK next to a vector store is retrieval-augmented generation in
        // all but name; neither on its own shows it.
        if (skills.Contains("LLM APIs") && skills.Contains("Vector search")) skills.Add("RAG");

        return skills;
    }

    private static IEnumerable<string> FromPackageJson(string content)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            return [];
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object) return [];

            return new[] { "dependencies", "devDependencies" }
                .Where(section => document.RootElement.TryGetProperty(section, out var value) && value.ValueKind == JsonValueKind.Object)
                .SelectMany(section => document.RootElement.GetProperty(section).EnumerateObject())
                .SelectMany(package => NpmPackages[package.Name])
                .ToList();
        }
    }

    private static IEnumerable<string> FromProjectFile(string content)
    {
        var skills = PackageReference().Matches(content)
            .Select(match => match.Groups[1].Value)
            .SelectMany(id => NuGetPackages
                .Where(package => id.StartsWith(package.Prefix, StringComparison.OrdinalIgnoreCase))
                .Select(package => package.Skill))
            .ToList();

        if (content.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase)) skills.Add("ASP.NET Core");
        return skills;
    }

    // requirements.txt and pyproject.toml spell dependencies differently, but in both a
    // distribution name starts a line or a quoted string and ends at a version marker.
    private static IEnumerable<string> FromPythonManifest(string content) =>
        PythonDependency().Matches(content)
            .Select(match => match.Groups[1].Value.ToLowerInvariant().Replace('_', '-'))
            .SelectMany(name => PythonPackages[name]);

    private static string? ManifestKind(string path)
    {
        var file = path[(path.LastIndexOf('/') + 1)..];
        if (file.Equals("package.json", StringComparison.OrdinalIgnoreCase)) return "npm";
        if (file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) return "nuget";
        if (file.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase)) return "python";
        if (file.StartsWith("requirements", StringComparison.OrdinalIgnoreCase) &&
            file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) return "python";
        return null;
    }

    private static int Depth(string path) => path.Count(character => character == '/');

    private static bool IsVendored(string path) => Vendored().IsMatch(path);

    private static Regex Rx(string pattern) =>
        new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static ILookup<string, string> Lookup(params (string Package, string Skill)[] pairs) =>
        pairs.ToLookup(pair => pair.Package, pair => pair.Skill, StringComparer.Ordinal);

    // Dependencies, build output and generated code say nothing about what the author wrote.
    [GeneratedRegex(
        @"(^|/)(node_modules|bin|obj|dist|build|vendor|\.venv|venv|__pycache__|\.next|coverage|Migrations)/|(^|/)wwwroot/lib/|\.min\.(js|css)$|\.d\.ts$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Vendored();

    [GeneratedRegex(@"<PackageReference\s+Include=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PackageReference();

    [GeneratedRegex(@"(?:^|[""'])\s*([A-Za-z0-9][A-Za-z0-9._-]*)\s*(?=[=<>~!\[;""',\s]|$)", RegexOptions.Multiline)]
    private static partial Regex PythonDependency();
}
