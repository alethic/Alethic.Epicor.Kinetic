using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Alethic.Epicor.Kinetic.Client.Internal;

namespace Alethic.Epicor.Kinetic.Client;

/// <inheritdoc cref="IKineticBaqClient"/>
public sealed class KineticBaqClient : IKineticBaqClient
{

    sealed class ODataCollection<T>
    {

        /// <summary>
        /// The rows of the OData collection payload.
        /// </summary>
        [JsonPropertyName("value")]
        public List<T>? Value { get; init; }

    }

    readonly HttpClient _http;
    readonly KineticOptions _options;
    readonly ILogger<KineticBaqClient> _logger;

    /// <summary>
    /// Creates a client over an HttpClient that carries the <see cref="KineticHttpHandler"/>.
    /// </summary>
    public KineticBaqClient(HttpClient httpClient, IOptions<KineticOptions> options, ILogger<KineticBaqClient> logger)
    {
        _http = Guard.NotNull(httpClient, nameof(httpClient));
        _options = Guard.NotNull(options, nameof(options)).Value;
        _logger = Guard.NotNull(logger, nameof(logger));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<TRow>> QueryAsync<TRow>(string baqId, BaqQuery? query, CancellationToken cancellationToken)
    {
        return QueryAsync<TRow>(baqId, query, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TRow>> QueryAsync<TRow>(
        string baqId,
        BaqQuery? query = null,
        KineticCallSettings? callSettings = null,
        CancellationToken cancellationToken = default)
    {
        var uri = DataUri(baqId, query?.ToQueryString() ?? string.Empty);
        var json = _options.JsonSerializerOptions;
        var page = await KineticHttp.SendJsonAsync<ODataCollection<TRow>>(
            _http,
            _logger,
            HttpMethod.Get,
            uri,
            null,
            callSettings,
            json,
            cancellationToken).ConfigureAwait(false);

        return page?.Value ?? new List<TRow>();
    }

    /// <inheritdoc/>
    public Task<TRow?> AddRowAsync<TRow>(string baqId, TRow row, CancellationToken cancellationToken)
    {
        return AddRowAsync(baqId, row, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TRow?> AddRowAsync<TRow>(
        string baqId,
        TRow row,
        KineticCallSettings? callSettings = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull((object?)row, nameof(row));
        var uri = DataUri(baqId, string.Empty);
        var json = _options.JsonSerializerOptions;
        return KineticHttp.SendJsonAsync<TRow>(_http, _logger, HttpMethod.Post, uri, row, callSettings, json, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TRow?> UpdateRowAsync<TRow>(string baqId, TRow row, CancellationToken cancellationToken)
    {
        return UpdateRowAsync(baqId, row, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TRow?> UpdateRowAsync<TRow>(
        string baqId,
        TRow row,
        KineticCallSettings? callSettings = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull((object?)row, nameof(row));
        var uri = DataUri(baqId, string.Empty);
        var json = _options.JsonSerializerOptions;
        return KineticHttp.SendJsonAsync<TRow>(_http, _logger, KineticHttp.Patch, uri, row, callSettings, json, cancellationToken);
    }

    Uri DataUri(string baqId, string queryString)
    {
        Guard.NotNullOrWhiteSpace(baqId, nameof(baqId));
        var company = KineticHttp.Segment(_options.Company);
        return KineticHttp.Combine(_http, _options, $"api/v2/odata/{company}/BaqSvc/{KineticHttp.Segment(baqId)}/Data{queryString}");
    }

}
