using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OData.Client;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Alethic.Epicor.Kinetic.Client.Internal;

namespace Alethic.Epicor.Kinetic.Client;

/// <inheritdoc cref="IKineticODataContextFactory"/>
public sealed partial class KineticODataContextFactory : IKineticODataContextFactory
{

    static partial class Log
    {

        [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Kinetic OData context configured for {ServiceRoot}")]
        public static partial void ContextConfigured(ILogger logger, Uri serviceRoot);

        [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Kinetic OData metadata loaded for {ServiceRoot}")]
        public static partial void MetadataLoaded(ILogger logger, Uri serviceRoot);

    }

    readonly IHttpClientFactory _httpClientFactory;
    readonly KineticOptions _options;
    readonly ILogger<KineticODataContextFactory> _logger;
    readonly ConcurrentDictionary<Uri, Lazy<IEdmModel>> _models = new();

    /// <summary>
    /// Creates a factory whose contexts send through the named Kinetic HttpClient.
    /// </summary>
    public KineticODataContextFactory(
        IHttpClientFactory httpClientFactory,
        IOptions<KineticOptions> options,
        ILogger<KineticODataContextFactory> logger)
    {
        _httpClientFactory = Guard.NotNull(httpClientFactory, nameof(httpClientFactory));
        _options = Guard.NotNull(options, nameof(options)).Value;
        _logger = Guard.NotNull(logger, nameof(logger));
    }

    /// <inheritdoc/>
    public DataServiceContext ForService(string service)
    {
        return ForService(service, null);
    }

    /// <inheritdoc/>
    public DataServiceContext ForService(string service, KineticCallSettings? callSettings = null)
    {
        return Configure(new DataServiceContext(ServiceRoot(service), ODataProtocolVersion.V4), callSettings);
    }

    /// <inheritdoc/>
    public DataServiceContext ForBaq(string baqId)
    {
        return ForBaq(baqId, null);
    }

    /// <inheritdoc/>
    public DataServiceContext ForBaq(string baqId, KineticCallSettings? callSettings = null)
    {
        Guard.NotNullOrWhiteSpace(baqId, nameof(baqId));
        var company = KineticHttp.Segment(_options.Company);
        var root = new Uri(_options.GetBaseUri(), $"api/v2/odata/{company}/BaqSvc/{KineticHttp.Segment(baqId)}/");
        return Configure(new DataServiceContext(root, ODataProtocolVersion.V4), callSettings);
    }

    /// <inheritdoc/>
    public Uri ServiceRoot(string service)
    {
        Guard.NotNullOrWhiteSpace(service, nameof(service));
        var company = KineticHttp.Segment(_options.Company);
        return new Uri(_options.GetBaseUri(), $"api/v2/odata/{company}/{KineticHttp.Segment(service)}/");
    }

    /// <summary>
    /// Routes the context through the authenticated pipeline and supplies its service model from a per-root cache.
    /// The model is downloaded from <c>$metadata</c> on first use and shared by every context for that service root.
    /// </summary>
    public TContext Configure<TContext>(TContext context)
        where TContext : DataServiceContext
    {
        return Configure(context, null);
    }

    /// <summary>
    /// Routes the context through the authenticated pipeline, every request carrying the given call settings, and
    /// supplies its service model from a per-root cache. The model is downloaded from <c>$metadata</c> on first use and
    /// shared by every context for that service root.
    /// </summary>
    public TContext Configure<TContext>(TContext context, KineticCallSettings? callSettings = null)
        where TContext : DataServiceContext
    {
        Guard.NotNull(context, nameof(context));
        context.Configurations.RequestPipeline.OnMessageCreating = args => CreateMessage(args, callSettings);
        context.Format.LoadServiceModel = () => LoadModel(context.BaseUri);
        Log.ContextConfigured(_logger, context.BaseUri);
        return context;
    }

    DataServiceClientRequestMessage CreateMessage(DataServiceClientRequestMessageArgs args, KineticCallSettings? callSettings)
    {
        var http = _httpClientFactory.CreateClient(KineticServiceCollectionExtensions.HttpClientName);
        return new KineticODataRequestMessage(args, http, callSettings);
    }

    IEdmModel LoadModel(Uri serviceRoot)
    {
        return _models.GetOrAdd(serviceRoot, root => new Lazy<IEdmModel>(() => FetchModel(root))).Value;
    }

    IEdmModel FetchModel(Uri serviceRoot)
    {
        var http = _httpClientFactory.CreateClient(KineticServiceCollectionExtensions.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(serviceRoot, "$metadata"));
        request.Headers.Accept.ParseAdd("application/xml");

        using var response = http.SendAsync(request).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
            throw KineticApiException.CreateAsync(request, response).GetAwaiter().GetResult();

        using var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
        using var reader = XmlReader.Create(stream);
        var model = CsdlReader.Parse(reader);
        Log.MetadataLoaded(_logger, serviceRoot);
        return model;
    }

}
