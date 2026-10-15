using System;
using System.Collections.Generic;

namespace Alethic.Epicor.Kinetic.Client;

/// <summary>
/// OData query options for reading an entity set: <c>$filter</c>, <c>$select</c>, <c>$orderby</c>, <c>$top</c> and
/// <c>$skip</c>.
/// </summary>
public record ODataQuery
{

    /// <summary>
    /// Adds an option to the query string parts when it has a value.
    /// </summary>
    static void Add(List<string> parts, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            parts.Add($"{key}={Uri.EscapeDataString(value)}");
    }

    /// <summary>
    /// OData <c>$filter</c> expression over the entity set's columns, for example <c>OrderNum eq 5001</c>.
    /// </summary>
    public string? Filter { get; init; }

    /// <summary>
    /// Comma-separated columns to return, as OData <c>$select</c>.
    /// </summary>
    public string? Select { get; init; }

    /// <summary>
    /// OData <c>$orderby</c> expression.
    /// </summary>
    public string? OrderBy { get; init; }

    /// <summary>
    /// Maximum number of rows to return, as OData <c>$top</c>.
    /// </summary>
    public int? Top { get; init; }

    /// <summary>
    /// Number of rows to skip, as OData <c>$skip</c>.
    /// </summary>
    public int? Skip { get; init; }

    /// <summary>
    /// Renders the options as a URL query string starting with <c>?</c>, or an empty string when nothing is set.
    /// </summary>
    internal string ToQueryString()
    {
        var parts = new List<string>();
        AddTo(parts);
        return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
    }

    /// <summary>
    /// Adds the options that are set to the query string parts.
    /// </summary>
    internal virtual void AddTo(List<string> parts)
    {
        Add(parts, "$filter", Filter);
        Add(parts, "$select", Select);
        Add(parts, "$orderby", OrderBy);

        if (Top is { } top)
            parts.Add($"$top={top}");

        if (Skip is { } skip)
            parts.Add($"$skip={skip}");
    }

}
