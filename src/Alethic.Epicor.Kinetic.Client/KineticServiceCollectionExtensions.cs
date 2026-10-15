using System;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Alethic.Epicor.Kinetic.Client;

/// <summary>
/// Registers the Kinetic clients with dependency injection.
/// </summary>
public static class KineticServiceCollectionExtensions
{

    /// <summary>
    /// Name of the general-purpose authenticated HttpClient, usable via <see cref="IHttpClientFactory"/> for any Kinetic
    /// URL.
    /// </summary>
    public const string HttpClientName = "Alethic.Epicor.Kinetic";

    /// <summary>
    /// Registers the clients, binding <see cref="KineticOptions"/> from a configuration section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration section holding the <see cref="KineticOptions"/> values.</param>
    /// <param name="configureHttpClient">
    /// Optional hook applied to every Kinetic HttpClient registration, for example to add resilience handlers.
    /// </param>
    public static IServiceCollection AddKineticClient(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IHttpClientBuilder>? configureHttpClient = null)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        if (configuration is null)
            throw new ArgumentNullException(nameof(configuration));

        services.Configure<KineticOptions>(configuration);
        return services.AddKineticClientCore(configureHttpClient);
    }

    /// <summary>
    /// Registers the clients, configuring <see cref="KineticOptions"/> in code.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Populates the <see cref="KineticOptions"/>.</param>
    /// <param name="configureHttpClient">
    /// Optional hook applied to every Kinetic HttpClient registration, for example to add resilience handlers.
    /// </param>
    public static IServiceCollection AddKineticClient(
        this IServiceCollection services,
        Action<KineticOptions> configure,
        Action<IHttpClientBuilder>? configureHttpClient = null)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        services.Configure(configure);
        return services.AddKineticClientCore(configureHttpClient);
    }

    static IServiceCollection AddKineticClientCore(this IServiceCollection services, Action<IHttpClientBuilder>? configureHttpClient)
    {
        services.AddOptions<KineticOptions>().ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<KineticOptions>, KineticOptionsValidator>());
        services.TryAddTransient<KineticHttpHandler>();

        Configure(services.AddHttpClient(HttpClientName), configureHttpClient);
        Configure(services.AddHttpClient<IKineticFunctionClient, KineticFunctionClient>(), configureHttpClient);
        Configure(services.AddHttpClient<IKineticServiceClient, KineticServiceClient>(), configureHttpClient);
        Configure(services.AddHttpClient<IKineticBaqClient, KineticBaqClient>(), configureHttpClient);

        services.TryAddSingleton<IKineticODataContextFactory, KineticODataContextFactory>();
        return services;
    }

    static void Configure(IHttpClientBuilder builder, Action<IHttpClientBuilder>? configureHttpClient)
    {
        builder
            .ConfigureHttpClient((provider, http) =>
            {
                var options = provider.GetRequiredService<IOptions<KineticOptions>>().Value;
                http.BaseAddress = options.GetBaseUri();
                http.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddHttpMessageHandler<KineticHttpHandler>();

        configureHttpClient?.Invoke(builder);
    }

}
