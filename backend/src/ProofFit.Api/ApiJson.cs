using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProofFit.Api;

/// <summary>
/// The one JSON shape used everywhere: HTTP responses, stored analysis results, and tests
/// that read them back. Enums travel as camelCase strings so stored data stays readable
/// and survives reordering of enum members.
/// </summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
