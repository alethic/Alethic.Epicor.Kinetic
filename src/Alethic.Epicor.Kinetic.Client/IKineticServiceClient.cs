using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Alethic.Epicor.Kinetic.Client;

/// <summary>
/// Calls any business object service method: <c>POST api/v2/odata/{company}/{service}/{method}</c>. This is the generic
/// layer; typed wrappers for specific services can be built on it.
/// </summary>
public interface IKineticServiceClient
{

    /// <summary>
    /// Calls a service method such as <c>Erp.BO.CustomerSvc/GetByID</c>. The parameters object is serialised as the
    /// method's named parameters (camelCase, for example <c>new { custNum = 1 }</c>). The response envelope is
    /// <c>{ returnObj, parameters }</c>; see <see cref="BoResponse{TReturn}"/>.
    /// </summary>
    Task<TResponse?> CallAsync<TResponse>(
        string service,
        string method,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls a service method such as <c>Erp.BO.CustomerSvc/GetByID</c> with call settings of its own, such as another
    /// plant. Values the call settings leave null keep the configured ones.
    /// </summary>
    Task<TResponse?> CallAsync<TResponse>(
        string service,
        string method,
        object? parameters,
        KineticCallSettings? callSettings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls a service method and discards the response.
    /// </summary>
    Task CallAsync(string service, string method, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls a service method with call settings of its own and discards the response.
    /// </summary>
    Task CallAsync(
        string service,
        string method,
        object? parameters,
        KineticCallSettings? callSettings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an arbitrary JSON request relative to the instance root, for endpoints the shaped methods do not cover.
    /// </summary>
    Task<TResponse?> SendAsync<TResponse>(
        HttpMethod httpMethod,
        string relativePath,
        object? body = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an arbitrary JSON request relative to the instance root with call settings of its own, such as an OData
    /// query in another plant. Values the call settings leave null keep the configured ones.
    /// </summary>
    Task<TResponse?> SendAsync<TResponse>(
        HttpMethod httpMethod,
        string relativePath,
        object? body,
        KineticCallSettings? callSettings,
        CancellationToken cancellationToken = default);

}
