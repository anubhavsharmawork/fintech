using TransactionService.Data;

namespace TransactionService.Services;

/// <summary>
/// Abstraction for the bounded AML screening channel.
/// Allows transaction creation to enqueue screening work without blocking.
/// </summary>
public interface IAmlScreeningChannel
{
    /// <summary>
    /// Attempts to enqueue a transaction for asynchronous AML screening.
    /// Returns true if the transaction was queued; false if the channel is full.
    /// </summary>
    bool TryEnqueue(Transaction transaction);

    /// <summary>
    /// Asynchronously reads the next transaction from the channel.
    /// Used by the background worker.
    /// </summary>
    ValueTask<Transaction> DequeueAsync(CancellationToken cancellationToken);
}
