namespace TransactionService.Models;

/// <summary>
/// Stores idempotency keys with their responses to prevent duplicate transaction creation.
/// Keys expire after 24 hours via TTL.
/// </summary>
public class IdempotencyRecord
{
    public Guid Id { get; set; }

    /// <summary>
    /// The unique idempotency key provided by the client.
    /// </summary>
    public string IdempotencyKey { get; set; } = null!;

    /// <summary>
    /// The HTTP path this key was used for.
    /// </summary>
    public string RequestPath { get; set; } = null!;

    /// <summary>
    /// The HTTP method (POST, PUT, etc.).
    /// </summary>
    public string RequestMethod { get; set; } = null!;

    /// <summary>
    /// The stored response body as JSON.
    /// </summary>
    public string ResponseBody { get; set; } = null!;

    /// <summary>
    /// The HTTP status code of the original response.
    /// </summary>
    public int ResponseStatusCode { get; set; }

    /// <summary>
    /// True while the original request is still being processed.
    /// Used to return 409 Conflict on concurrent duplicate requests.
    /// </summary>
    public bool IsProcessing { get; set; }

    /// <summary>
    /// When the record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this record expires (CreatedAt + 24 hours).
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
