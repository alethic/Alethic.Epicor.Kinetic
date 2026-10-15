using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Alethic.Epicor.Kinetic.Client.Internal;

namespace Alethic.Epicor.Kinetic.Client;

/// <summary>
/// Bottom layer. Turns any <see cref="HttpClient"/> into an authenticated Kinetic client by adding the API key,
/// credentials, a JSON Accept header and the optional CallSettings header, and by logging each request at Debug
/// level. Every other class in this library sits on top of it, including the OData contexts.
/// </summary>
public sealed partial class KineticHttpHandler : DelegatingHandler
{

    static partial class Log
    {

        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Kinetic {Method} {Uri} responded {StatusCode} in {ElapsedMs:F0} ms")]
        public static partial void RequestCompleted(ILogger logger, string method, Uri? uri, int statusCode, double elapsedMs);

    }

    static readonly MediaTypeWithQualityHeaderValue _jsonMediaType = new("application/json");

    /// <summary>
    /// Builds the CallSettings header value from the configured values, each replaced by the call's own when it has
    /// one. Returns <c>null</c> when there is no plant, language or format culture to send.
    /// </summary>
    static string? BuildCallSettings(KineticOptions options, KineticCallSettings? call)
    {
        var plant = Pick(call?.Plant, options.Plant);
        var language = Pick(call?.Language, options.Language);
        var formatCulture = Pick(call?.FormatCulture, options.FormatCulture);
        if (plant is null && language is null && formatCulture is null)
            return null;

        var settings = new Dictionary<string, string> { ["Company"] = options.Company };
        if (plant is not null)
            settings["Plant"] = plant;

        if (language is not null)
            settings["Language"] = language;

        if (formatCulture is not null)
            settings["FormatCulture"] = formatCulture;

        return JsonSerializer.Serialize(settings);
    }

    /// <summary>
    /// Returns the call's value when it has one, else the configured value when that is not empty, else <c>null</c>.
    /// </summary>
    static string? Pick(string? call, string? configured)
    {
        if (!string.IsNullOrEmpty(call))
            return call;

        return string.IsNullOrEmpty(configured) ? null : configured;
    }

    readonly KineticOptions _options;
    readonly ILogger<KineticHttpHandler> _logger;
    readonly string? _basicCredentials;
    readonly string? _callSettings;

    /// <summary>
    /// Creates a handler that authenticates with the given options.
    /// </summary>
    public KineticHttpHandler(IOptions<KineticOptions> options, ILogger<KineticHttpHandler> logger)
    {
        _options = Guard.NotNull(options, nameof(options)).Value;
        _logger = Guard.NotNull(logger, nameof(logger));

        if (_options.AccessTokenProvider is null)
            _basicCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));

        _callSettings = BuildCallSettings(_options, null);
    }

    /// <summary>
    /// Adds the Kinetic headers when the request does not already carry them, sends it, and logs the outcome.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization ??= await CreateAuthorizationAsync(cancellationToken).ConfigureAwait(false);

        if (!request.Headers.Contains("x-api-key"))
            request.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);

        if (request.Headers.Accept.Count == 0)
            request.Headers.Accept.Add(_jsonMediaType);

        if (!request.Headers.Contains("CallSettings"))
        {
            var call = KineticHttp.GetCallSettings(request);
            var callSettings = call is null ? _callSettings : BuildCallSettings(_options, call);
            if (callSettings is not null)
                request.Headers.TryAddWithoutValidation("CallSettings", callSettings);
        }

        var start = Stopwatch.GetTimestamp();
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var elapsedMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        Log.RequestCompleted(_logger, request.Method.Method, request.RequestUri, (int)response.StatusCode, elapsedMs);
        return response;
    }

    async ValueTask<AuthenticationHeaderValue> CreateAuthorizationAsync(CancellationToken cancellationToken)
    {
        if (_options.AccessTokenProvider is { } provider)
            return new AuthenticationHeaderValue("Bearer", await provider(cancellationToken).ConfigureAwait(false));

        return new AuthenticationHeaderValue("Basic", _basicCredentials);
    }

}
