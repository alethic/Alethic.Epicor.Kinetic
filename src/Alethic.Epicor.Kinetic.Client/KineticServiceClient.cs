using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Alethic.Epicor.Kinetic.Client.Internal;

namespace Alethic.Epicor.Kinetic.Client;

/// <inheritdoc cref="IKineticServiceClient"/>
public sealed class KineticServiceClient : IKineticServiceClient
{

    readonly HttpClient _http;
    readonly KineticOptions _options;
    readonly ILogger<KineticServiceClient> _logger;

    /// <summary>
    /// Creates a client over an HttpClient that carries the <see cref="KineticHttpHandler"/>.
    /// </summary>
    public KineticServiceClient(HttpClient httpClient, IOptions<KineticOptions> options, ILogger<KineticServiceClient> logger)
    {
        _http = Guard.NotNull(httpClient, nameof(httpClient));
        _options = Guard.NotNull(options, nameof(options)).Value;
        _logger = Guard.NotNull(logger, nameof(logger));
    }

    /// <inheritdoc/>
    public Task<TResponse?> CallAsync<TResponse>(string service, string method, object? parameters, CancellationToken cancellationToken)
    {
        return CallAsync<TResponse>(service, method, parameters, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TResponse?> CallAsync<TResponse>(
        string service,
        string method,
        object? parameters = null,
        KineticCallSettings? callSettings = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(service, nameof(service));
        Guard.NotNullOrWhiteSpace(method, nameof(method));

        var company = KineticHttp.Segment(_options.Company);
        var path = $"api/v2/odata/{company}/{KineticHttp.Segment(service)}/{KineticHttp.Segment(method)}";
        return SendAsync<TResponse>(HttpMethod.Post, path, parameters ?? KineticHttp.EmptyBody, callSettings, cancellationToken);
    }

    /// <inheritdoc/>
    public Task CallAsync(string service, string method, object? parameters, CancellationToken cancellationToken)
    {
        return CallAsync<JsonElement>(service, method, parameters, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task CallAsync(
        string service,
        string method,
        object? parameters = null,
        KineticCallSettings? callSettings = null,
        CancellationToken cancellationToken = default)
    {
        return CallAsync<JsonElement>(service, method, parameters, callSettings, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<TRow>> QueryAsync<TRow>(
        string service,
        string entitySet,
        ODataQuery? query = null,
        KineticCallSettings? callSettings = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(service, nameof(service));
        Guard.NotNullOrWhiteSpace(entitySet, nameof(entitySet));

        var company = KineticHttp.Segment(_options.Company);
        var path = $"api/v2/odata/{company}/{KineticHttp.Segment(service)}/{KineticHttp.Segment(entitySet)}{query?.ToQueryString()}";
        var uri = KineticHttp.Combine(_http, _options, path);
        return KineticHttp.QueryAllAsync<TRow>(_http, _logger, uri, callSettings, _options.JsonSerializerOptions, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TResponse?> SendAsync<TResponse>(
        HttpMethod httpMethod,
        string relativePath,
        object? body,
        CancellationToken cancellationToken)
    {
        return SendAsync<TResponse>(httpMethod, relativePath, body, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TResponse?> SendAsync<TResponse>(
        HttpMethod httpMethod,
        string relativePath,
        object? body = null,
        KineticCallSettings? callSettings = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(httpMethod, nameof(httpMethod));
        Guard.NotNullOrWhiteSpace(relativePath, nameof(relativePath));

        var uri = KineticHttp.Combine(_http, _options, relativePath);
        return KineticHttp.SendJsonAsync<TResponse>(
            _http,
            _logger,
            httpMethod,
            uri,
            body,
            callSettings,
            _options.JsonSerializerOptions,
            cancellationToken);
    }

}
