using System.Threading;
using System.Threading.Tasks;

namespace Alethic.Epicor.Kinetic.Client;

/// <summary>
/// Invokes Epicor Functions: <c>POST api/v2/efx/{company}/{library}/{function}</c>.
/// </summary>
public interface IKineticFunctionClient
{

    /// <summary>
    /// Invokes a function and deserialises its response parameters into <typeparamref name="TResponse"/>. The request
    /// object is serialised as the function's input parameters; pass <c>null</c> for none.
    /// </summary>
    Task<TResponse?> InvokeAsync<TResponse>(string library, string function, object? request, CancellationToken cancellationToken);

    /// <summary>
    /// Invokes a function and deserialises its response parameters into <typeparamref name="TResponse"/>, optionally
    /// with call settings of its own, such as another plant; the function runs in a session with those settings. The
    /// request object is serialised as the function's input parameters; pass <c>null</c> for none. Values the call
    /// settings leave null keep the configured ones.
    /// </summary>
    Task<TResponse?> InvokeAsync<TResponse>(
        string library,
        string function,
        object? request = null,
        KineticCallSettings? callSettings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a function and discards its response.
    /// </summary>
    Task InvokeAsync(string library, string function, object? request, CancellationToken cancellationToken);

    /// <summary>
    /// Invokes a function, optionally with call settings of its own, and discards its response.
    /// </summary>
    Task InvokeAsync(
        string library,
        string function,
        object? request = null,
        KineticCallSettings? callSettings = null,
        CancellationToken cancellationToken = default);

}
