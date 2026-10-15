using System;

using Alethic.Epicor.Kinetic.Client.Internal;

namespace Alethic.Epicor.Kinetic.Client;

/// <summary>
/// CallSettings values for a single call. Each value given replaces the configured one in <see cref="KineticOptions"/>
/// for that call only; a value left null keeps the configured one. Use it to work in another plant for one call, such
/// as reading a business object that scopes its rows to the session's plant, or to give one slow call more time.
/// </summary>
public sealed record KineticCallSettings
{

    readonly TimeSpan? _timeout;

    /// <summary>
    /// Plant (site) for the call. Kinetic takes a single plant; a plant it does not accept falls back to the user's
    /// default plant without an error.
    /// </summary>
    public string? Plant { get; init; }

    /// <summary>
    /// Language for the call.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Format culture for the call.
    /// </summary>
    public string? FormatCulture { get; init; }

    /// <summary>
    /// How long the call may take, in place of <see cref="KineticOptions.Timeout"/>. It may be longer or shorter than
    /// the configured timeout. <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> lets the call run until it completes
    /// or is cancelled. Not sent to Kinetic; the client enforces it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero, negative or too large and not infinite.</exception>
    public TimeSpan? Timeout
    {
        get => _timeout;
        init
        {
            if (value is { } timeout && !KineticHttp.IsValidTimeout(timeout))
                throw new ArgumentOutOfRangeException(nameof(Timeout), timeout, "Timeout must be positive and fit Int32 ms, or infinite.");

            _timeout = value;
        }
    }

}
