using System.Threading.Channels;
using PersonalFinance.Application.DTOs.Email;
using PersonalFinance.Application.Interfaces;

namespace PersonalFinance.Infrastructure.Services;

/// <summary>
/// Fila de e-mails em memória, limitada. Fila cheia descarta o excedente (DropWrite) sem bloquear nem lançar.
/// </summary>
public sealed class ChannelEmailQueue : IEmailQueue
{
    private const int DefaultCapacity = 1000;

    private readonly Channel<EmailMessage> _channel;

    public ChannelEmailQueue() : this(DefaultCapacity)
    {
    }

    public ChannelEmailQueue(int capacity)
    {
        _channel = Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public bool TryEnqueue(EmailMessage message) => _channel.Writer.TryWrite(message);

    public ValueTask<EmailMessage> DequeueAsync(CancellationToken ct) => _channel.Reader.ReadAsync(ct);
}
