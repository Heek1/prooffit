using System.Reflection;
using NetArchTest.Rules;
using ProofFit.Api.Domain;

namespace ProofFit.Api.Tests;

/// <summary>
/// Keeps the dependencies between the API's parts pointing one way: features build on the
/// domain, the matching core stays free of storage and the web, and features reach one
/// another only where the design says so.
/// </summary>
public class ArchitectureTests
{
    private const string Root = "ProofFit.Api";
    private const string Domain = Root + ".Domain";
    private const string Data = Root + ".Data";
    private const string Features = Root + ".Features";
    private const string Analyses = Features + ".Analyses";
    private const string Matching = Analyses + ".Matching";
    private const string GitHub = Features + ".GitHub";
    private const string Llm = Features + ".Llm";
    private const string Resumes = Features + ".Resumes";
    private const string Vacancies = Features + ".Vacancies";

    private const string EntityFramework = "Microsoft.EntityFrameworkCore";
    private const string AspNetCore = "Microsoft.AspNetCore";

    private static readonly Assembly Api = typeof(Analysis).Assembly;

    /// <summary>
    /// The analysis feature's own types, outside the matching core: its endpoints, runner and
    /// worker. Named one by one, since a namespace rule would also take in Matching.
    /// </summary>
    private static readonly string[] AnalysisPipeline = Api.GetTypes()
        .Where(type => type.Namespace == Analyses)
        .Select(type => type.FullName!)
        .ToArray();

    [Fact]
    public void The_domain_depends_on_nothing_else_in_the_API_nor_on_storage_or_the_web()
    {
        AssertNoDependency(Types.InAssembly(Api).That().ResideInNamespace(Domain), Data, Features, EntityFramework, AspNetCore);
    }

    [Fact]
    public void Storage_does_not_depend_on_any_feature()
    {
        AssertNoDependency(Types.InAssembly(Api).That().ResideInNamespace(Data), Features);
    }

    [Fact]
    public void The_matching_core_knows_nothing_of_storage_the_web_or_other_features()
    {
        AssertNoDependency(
            Types.InAssembly(Api).That().ResideInNamespace(Matching),
            Data, EntityFramework, AspNetCore, GitHub, Resumes, Vacancies);
        Assert.NotEmpty(AnalysisPipeline);
        AssertNoDependency(Types.InAssembly(Api).That().ResideInNamespace(Matching), AnalysisPipeline);
    }

    [Fact]
    public void The_matching_core_talks_to_a_model_only_through_its_interface()
    {
        AssertNoDependency(Types.InAssembly(Api).That().ResideInNamespace(Matching), Llm + ".GeminiClient");
    }

    [Fact]
    public void The_model_client_depends_on_no_other_feature_and_no_storage()
    {
        AssertNoDependency(Types.InAssembly(Api).That().ResideInNamespace(Llm), Data, Analyses, GitHub, Resumes, Vacancies);
    }

    [Fact]
    public void Resumes_and_vacancies_stand_alone()
    {
        AssertNoDependency(Types.InAssembly(Api).That().ResideInNamespace(Resumes), Analyses, GitHub, Llm, Vacancies);
        AssertNoDependency(Types.InAssembly(Api).That().ResideInNamespace(Vacancies), Analyses, GitHub, Llm, Resumes);
    }

    [Fact]
    public void GitHub_uses_the_skill_catalogue_but_not_the_analysis_pipeline()
    {
        AssertNoDependency(Types.InAssembly(Api).That().ResideInNamespace(GitHub), Llm, Resumes, Vacancies);
        AssertNoDependency(Types.InAssembly(Api).That().ResideInNamespace(GitHub), AnalysisPipeline);
    }

    private static void AssertNoDependency(PredicateList types, params string[] forbidden)
    {
        var result = types.ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Assert.True(result.IsSuccessful, $"These types depend on {string.Join(", ", forbidden)}: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
