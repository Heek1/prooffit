using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ProofFit.Api;

public class RateLimitOptions
{
    /// <summary>Resume uploads and saved vacancies allowed per client each minute.</summary>
    public int UploadsPerMinute { get; set; } = 30;

    /// <summary>Comparisons a client may start in ten minutes. Each one can cost two model calls.</summary>
    public int AnalysesPerTenMinutes { get; set; } = 10;
}

/// <summary>
/// Limits how fast one client can create things. The demo is public and every comparison
/// spends a small daily model quota, so without a limit one script could use it all up.
/// </summary>
public static class RateLimits
{
    public const string Uploads = "uploads";
    public const string Analyses = "analyses";

    public static IServiceCollection AddClientRateLimits(this IServiceCollection services)
    {
        services.AddOptions<RateLimitOptions>().BindConfiguration("RateLimit");

        // The API runs behind the host's proxy, which passes the caller's address in
        // X-Forwarded-For. Without this every visitor would share the proxy's address, and
        // with it one limit. The proxy is the only way in, so its header is trusted.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy(Uploads, http => Window(http, limits => limits.UploadsPerMinute, TimeSpan.FromMinutes(1)));
            limiter.AddPolicy(Analyses, http => Window(http, limits => limits.AnalysesPerTenMinutes, TimeSpan.FromMinutes(10)));

            limiter.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests. Wait a few minutes and try again.",
                    },
                    options: null, contentType: "application/problem+json", ct);
            };
        });

        return services;
    }

    private static RateLimitPartition<string> Window(HttpContext http, Func<RateLimitOptions, int> limit, TimeSpan window)
    {
        var limits = http.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        var client = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit(limits),
            Window = window,
        });
    }
}
