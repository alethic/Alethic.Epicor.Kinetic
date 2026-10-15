namespace Alethic.Epicor.Kinetic.Client;

/// <summary>
/// CallSettings values for a single call. Each value given replaces the configured one in <see cref="KineticOptions"/>
/// for that call only; a value left null keeps the configured one. Use it to work in another plant for one call, such
/// as reading a business object that scopes its rows to the session's plant.
/// </summary>
public sealed record KineticCallSettings
{

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

}
