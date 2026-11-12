using ProofFit.Api.Features.Analyses.Matching;
using ProofFit.Api.Features.GitHub;

namespace ProofFit.Api.Tests.GitHub;

public class RepositorySignalsTests
{
    [Fact]
    public void Reads_a_dotnet_api_from_its_files_and_project_references()
    {
        var skills = Detect(
            [
                "src/Api/Api.csproj", "src/Api/Program.cs", "src/Api/Orders.cs", "src/Api/Data/AppDbContext.cs",
                "tests/Api.Tests/OrdersTests.cs", "Dockerfile", ".github/workflows/ci.yml",
            ],
            ("src/Api/Api.csproj", """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <ItemGroup>
                    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
                    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
                  </ItemGroup>
                </Project>
                """));

        Assert.Equal(
            [".NET", "ASP.NET Core", "Authentication", "Automated testing", "C#", "CI/CD", "Docker",
             "Entity Framework Core", "PostgreSQL"],
            skills);
    }

    [Fact]
    public void Reads_a_react_frontend_from_package_json()
    {
        var skills = Detect(
            ["package.json", "src/App.tsx", "src/main.tsx", "src/api.ts", "tailwind.config.ts"],
            ("package.json", """
                {
                  "dependencies": { "react": "^19.0.0", "react-dom": "^19.0.0" },
                  "devDependencies": { "typescript": "~5.9.0", "vitest": "^3.0.0", "tailwindcss": "^4.0.0" }
                }
                """));

        Assert.Equal(["Automated testing", "React", "Tailwind CSS", "TypeScript"], skills);
    }

    [Theory]
    [InlineData("requirements.txt", "fastapi==0.115.0\ngoogle-generativeai>=0.8\npgvector\n# pytest is only a comment here\n")]
    [InlineData("pyproject.toml", "[project]\nname = \"assistant\"\ndependencies = [\"fastapi>=0.115\", \"google-generativeai\", \"pgvector\"]\n")]
    public void Reads_python_dependencies_and_infers_rag_from_a_model_sdk_with_a_vector_store(string manifest, string content)
    {
        var skills = Detect([manifest, "app/main.py", "app/rag.py", "app/models.py"], (manifest, content));

        Assert.Equal(["FastAPI", "LLM APIs", "Python", "RAG", "Vector search"], skills);
    }

    [Fact]
    public void Ignores_dependencies_build_output_and_bundled_libraries()
    {
        var skills = Detect(
        [
            "Program.cs", "Startup.cs", "Models/User.cs",
            "node_modules/react/index.js", "node_modules/react/cjs/react.js", "node_modules/react/jsx-runtime.js",
            "wwwroot/lib/jquery/jquery.js", "wwwroot/lib/bootstrap/bootstrap.js", "wwwroot/lib/bootstrap/bootstrap.min.js",
            "bin/Debug/app.js", "obj/project.assets.js", "dist/bundle.js",
        ]);

        Assert.Equal(["C#"], skills);
    }

    [Fact]
    public void Does_not_claim_a_language_from_one_stray_file()
    {
        var skills = Detect(["a.cs", "b.cs", "c.cs", "d.cs", "e.cs", "f.cs", "g.cs", "wwwroot/js/site.js"]);

        Assert.Equal(["C#"], skills);
    }

    [Fact]
    public void Claims_the_only_language_of_a_small_repository()
    {
        Assert.Equal(["Python"], Detect(["bot.py"]));
    }

    [Fact]
    public void Survives_a_manifest_that_is_not_valid_json()
    {
        Assert.Empty(Detect(["package.json"], ("package.json", "{ not json")));
    }

    [Fact]
    public void Reads_the_manifests_nearest_the_root_and_skips_vendored_ones()
    {
        var manifests = RepositorySignals.ManifestsToRead(
        [
            "a/b/c/d/deep/package.json",
            "node_modules/react/package.json",
            "frontend/package.json",
            "backend/src/Api/Api.csproj",
            "requirements-dev.txt",
            "README.md",
        ]);

        Assert.Equal(["requirements-dev.txt", "frontend/package.json", "backend/src/Api/Api.csproj"], manifests);
    }

    [Fact]
    public void Reads_at_most_eight_manifests()
    {
        var paths = Enumerable.Range(1, 20).Select(index => $"service{index:00}/package.json").ToList();

        Assert.Equal(8, RepositorySignals.ManifestsToRead(paths).Count);
    }

    [Fact]
    public void Every_skill_the_rules_produce_is_in_the_catalog()
    {
        // The keyword matcher pairs facts with requirements by catalog name, so a rule
        // with a misspelt skill would produce evidence nothing can ever cite.
        var catalog = SkillCatalog.All.Select(skill => skill.Name).ToHashSet();

        Assert.DoesNotContain(RepositorySignals.SkillNames(), name => !catalog.Contains(name));
    }

    private static string[] Detect(string[] paths, params (string Path, string Content)[] manifests) =>
        RepositorySignals.Detect(paths, manifests.ToDictionary(m => m.Path, m => m.Content)).Order(StringComparer.Ordinal).ToArray();
}
