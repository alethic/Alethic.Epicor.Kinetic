using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Alethic.Epicor.Kinetic.Client.Internal;

namespace Alethic.Epicor.Kinetic.Client;

/// <inheritdoc cref="IKineticFunctionClient"/>
public sealed class KineticFunctionClient : IKineticFunctionClient
{

    readonly HttpClient _http;
    readonly KineticOptions _options;
    readonly ILogger<KineticFunctionClient> _logger;

    /// <summary>
    /// Creates a client over an HttpClient that carries the <see cref="KineticHttpHandler"/>.
    /// </summary>
    public KineticFunctionClient(HttpClient httpClient, IOptions<KineticOptions> options, ILogger<KineticFunctionClient> logger)
    {
        _http = Guard.NotNull(httpClient, nameof(httpClient));
        _options = Guard.NotNull(options, nameof(options)).Value;
        _logger = Guard.NotNull(logger, nameof(logger));
    }

    /// <inheritdoc/>
    public Task<TResponse?> InvokeAsync<TResponse>(string library, string function, object? request, CancellationToken cancellationToken)
    {
        return InvokeAsync<TResponse>(library, function, request, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TResponse?> InvokeAsync<TResponse>(
        string library,
        string function,
        object? request = null,
        KineticCallSettings? callSettings = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(library, nameof(library));
        Guard.NotNullOrWhiteSpace(function, nameof(function));

        var company = KineticHttp.Segment(_options.Company);
        var uri = KineticHttp.Combine(_http, _options, $"api/v2/efx/{company}/{KineticHttp.Segment(library)}/{KineticHttp.Segment(function)}");
        var body = request ?? KineticHttp.EmptyBody;
        var json = _options.JsonSerializerOptions;

        return KineticHttp.SendJsonAsync<TResponse>(_http, _logger, HttpMethod.Post, uri, body, callSettings, json, cancellationToken);
    }

    /// <inheritdoc/>
    public Task InvokeAsync(string library, string function, object? request, CancellationToken cancellationToken)
    {
        return InvokeAsync<JsonElement>(library, function, request, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task InvokeAsync(
        string library,
        string function,
        object? request = null,
        KineticCallSettings? callSettings = null,
        CancellationToken cancellationToken = default)
    {
        return InvokeAsync<JsonElement>(library, function, request, callSettings, cancellationToken);
    }

}
