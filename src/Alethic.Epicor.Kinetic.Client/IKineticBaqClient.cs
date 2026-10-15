using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Alethic.Epicor.Kinetic.Client;

/// <summary>
/// Runs Business Activity Queries through <c>api/v2/odata/{company}/BaqSvc/{baqId}/Data</c>.
/// </summary>
public interface IKineticBaqClient
{

    /// <summary>
    /// Executes a BAQ and returns its rows.
    /// </summary>
    Task<IReadOnlyList<TRow>> QueryAsync<TRow>(string baqId, BaqQuery? query, CancellationToken cancellationToken);

    /// <summary>
    /// Executes a BAQ and returns its rows, optionally with call settings of its own, such as another plant. Values the
    /// call settings leave null keep the configured ones.
    /// </summary>
    Task<IReadOnlyList<TRow>> QueryAsync<TRow>(
        string baqId,
        BaqQuery? query = null,
        KineticCallSettings? callSettings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a row through an updatable BAQ. Returns the row as stored.
    /// </summary>
    Task<TRow?> AddRowAsync<TRow>(string baqId, TRow row, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a row through an updatable BAQ, optionally with call settings of its own. Returns the row as stored.
    /// </summary>
    Task<TRow?> AddRowAsync<TRow>(
        string baqId,
        TRow row,
        KineticCallSettings? callSettings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a row through an updatable BAQ. The row must include its key columns. Returns the row as stored.
    /// </summary>
    Task<TRow?> UpdateRowAsync<TRow>(string baqId, TRow row, CancellationToken cancellationToken);

    /// <summary>
    /// Updates a row through an updatable BAQ, optionally with call settings of its own. The row must include its key
    /// columns. Returns the row as stored.
    /// </summary>
    Task<TRow?> UpdateRowAsync<TRow>(
        string baqId,
        TRow row,
        KineticCallSettings? callSettings = null,
        CancellationToken cancellationToken = default);

}
