using System.Threading.Channels;
using TransactionService.Data;

namespace TransactionService.Services;

/// <summary>
/// Bounded channel implementation for AML screening.
/// Capacity of 1000 with BoundedChannelFullMode.DropWrite ensures 
/// backpressure is handled gracefully without blocking the caller.
/// </summary>
public sealed class AmlScreeningChannel : IAmlScreeningChannel
{
    private readonly Channel<Transaction> _channel;

    public AmlScreeningChannel()
    {
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<Transaction>(options);
    }

    /// <inheritdoc />
    public bool TryEnqueue(Transaction transaction)
    {
        return _channel.Writer.TryWrite(transaction);
    }

    /// <inheritdoc />
    public ValueTask<Transaction> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
