using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Alethic.Epicor.Kinetic.Client;

/// <summary>
/// Connection settings for one Epicor Kinetic app server instance and company. Bind from configuration
/// (section <see cref="SectionName"/>) or configure in code.
/// </summary>
public sealed class KineticOptions
{

    /// <summary>
    /// Default configuration section name.
    /// </summary>
    public const string SectionName = "Kinetic";

    /// <summary>
    /// App server instance root, for example <c>https://example.epicorsaas.com/prod/</c>.
    /// </summary>
    public string BaseAddress { get; set; } = string.Empty;

    /// <summary>
    /// Company ID. REST v2 puts it in every URL.
    /// </summary>
    public string Company { get; set; } = string.Empty;

    /// <summary>
    /// API key from API Key Maintenance. REST v2 rejects calls without one.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Kinetic user for HTTP Basic authentication. Ignored when <see cref="AccessTokenProvider"/> is set.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Password for <see cref="Username"/>.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Optional bearer token source (Epicor IdP, Entra ID). When set, requests carry <c>Authorization: Bearer</c>
    /// instead of Basic credentials.
    /// </summary>
    public Func<CancellationToken, ValueTask<string>>? AccessTokenProvider { get; set; }

    /// <summary>
    /// Optional plant (site) sent in the CallSettings header.
    /// </summary>
    public string? Plant { get; set; }

    /// <summary>
    /// Optional language sent in the CallSettings header.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Optional format culture sent in the CallSettings header.
    /// </summary>
    public string? FormatCulture { get; set; }

    /// <summary>
    /// How long a call may take unless its <see cref="KineticCallSettings.Timeout"/> says otherwise.
    /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> turns the limit off.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Serializer settings for request and response bodies. Defaults to <see cref="KineticJson.Default"/>.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = KineticJson.Default;

    /// <summary>
    /// Returns the base address normalised to end with a slash so relative API paths combine correctly.
    /// </summary>
    public Uri GetBaseUri()
    {
        var address = BaseAddress.EndsWith("/", StringComparison.Ordinal) ? BaseAddress : BaseAddress + "/";
        return new Uri(address, UriKind.Absolute);
    }

}
