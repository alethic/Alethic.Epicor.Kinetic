using System;
using System.Collections.Generic;

namespace Alethic.Epicor.Kinetic.Client;

/// <summary>
/// Query options for a BAQ call. OData options map to <c>$filter</c> and friends; <see cref="Parameters"/> are BAQ
/// parameters.
/// </summary>
public sealed record BaqQuery : ODataQuery
{

    /// <summary>
    /// BAQ parameters, sent by name in the query string.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }

    /// <summary>
    /// Adds the OData options that are set, then the BAQ parameters, to the query string parts.
    /// </summary>
    internal override void AddTo(List<string> parts)
    {
        base.AddTo(parts);

        if (Parameters is not null)
        {
            foreach (var parameter in Parameters)
                parts.Add($"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}");
        }
    }

}
