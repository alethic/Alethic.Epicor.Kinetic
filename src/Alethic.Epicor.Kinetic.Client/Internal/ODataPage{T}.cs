using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alethic.Epicor.Kinetic.Client.Internal;

/// <summary>
/// One page of an OData collection: the rows, and the link to the next page when the server pages.
/// </summary>
sealed class ODataPage<T>
{

    /// <summary>
    /// The rows of the page.
    /// </summary>
    [JsonPropertyName("value")]
    public List<T>? Value { get; init; }

    /// <summary>
    /// The next page, or <c>null</c> on the last.
    /// </summary>
    [JsonPropertyName("@odata.nextLink")]
    public string? NextLink { get; init; }

}
