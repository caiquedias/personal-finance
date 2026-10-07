using FluentAssertions;
using PersonalFinance.Application.DTOs.Email;
using PersonalFinance.Infrastructure.Services;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Services;

/// <summary>
/// ChannelEmailQueue (#404): fila em memória limitada (padrão 1000) que descarta o que não cabe (DropWrite) —
/// fila cheia nunca bloqueia nem lança, e as mensagens já enfileiradas são preservadas. FIFO; Dequeue aguarda e
/// respeita cancelamento.
/// </summary>
public class ChannelEmailQueueTests
{
    private static EmailMessage Msg(string subject) => new("ana@x.com", subject, "<p>corpo</p>");

    private static async Task AssertNothingMoreAsync(ChannelEmailQueue sut)
    {
        var pending = sut.DequeueAsync(CancellationToken.None).AsTask();
        await Task.Delay(150);
        pending.IsCompleted.Should().BeFalse("a mensagem excedente deve ter sido descartada");
        // Libera a leitura pendente para não vazar a tarefa
        sut.TryEnqueue(Msg("release"));
        (await pending.WaitAsync(TimeSpan.FromSeconds(5))).Subject.Should().Be("release");
    }

    [Fact(DisplayName = "TryEnqueue aceita e Dequeue devolve em ordem FIFO")]
    public async Task EnqueueDequeue_ShouldBeFifo()
    {
        var sut = new ChannelEmailQueue();

        sut.TryEnqueue(Msg("1")).Should().BeTrue();
        sut.TryEnqueue(Msg("2")).Should().BeTrue();
        sut.TryEnqueue(Msg("3")).Should().BeTrue();

        (await sut.DequeueAsync(CancellationToken.None)).Subject.Should().Be("1");
        (await sut.DequeueAsync(CancellationToken.None)).Subject.Should().Be("2");
        (await sut.DequeueAsync(CancellationToken.None)).Subject.Should().Be("3");
    }

    [Fact(DisplayName = "Fila cheia: não bloqueia nem lança, preserva as já enfileiradas e descarta a excedente")]
    public async Task TryEnqueue_WhenFull_ShouldNotThrowAndDropOverflow()
    {
        var sut = new ChannelEmailQueue(capacity: 2);
        sut.TryEnqueue(Msg("a")).Should().BeTrue();
        sut.TryEnqueue(Msg("b")).Should().BeTrue();

        var act = () => sut.TryEnqueue(Msg("c"));

        act.Should().NotThrow();
        (await sut.DequeueAsync(CancellationToken.None)).Subject.Should().Be("a");
        (await sut.DequeueAsync(CancellationToken.None)).Subject.Should().Be("b");
        await AssertNothingMoreAsync(sut);
    }

    [Fact(DisplayName = "Capacidade padrão é 1000: o 1001º é descartado")]
    public async Task DefaultCapacity_ShouldBeOneThousand()
    {
        var sut = new ChannelEmailQueue();

        for (var i = 0; i < 1001; i++)
            sut.TryEnqueue(Msg(i.ToString()));

        for (var i = 0; i < 1000; i++)
            (await sut.DequeueAsync(CancellationToken.None)).Subject.Should().Be(i.ToString());
        await AssertNothingMoreAsync(sut);
    }

    [Fact(DisplayName = "Dequeue libera espaço para novo TryEnqueue")]
    public async Task Dequeue_ShouldFreeSpace()
    {
        var sut = new ChannelEmailQueue(capacity: 1);
        sut.TryEnqueue(Msg("a"));
        sut.TryEnqueue(Msg("b")); // descartada

        (await sut.DequeueAsync(CancellationToken.None)).Subject.Should().Be("a");
        sut.TryEnqueue(Msg("c"));

        (await sut.DequeueAsync(CancellationToken.None)).Subject.Should().Be("c");
    }

    [Fact(DisplayName = "Dequeue aguarda até haver mensagem")]
    public async Task Dequeue_ShouldWaitForMessage()
    {
        var sut = new ChannelEmailQueue();

        var pending = sut.DequeueAsync(CancellationToken.None).AsTask();
        await Task.Delay(100);
        pending.IsCompleted.Should().BeFalse();

        sut.TryEnqueue(Msg("later"));

        (await pending.WaitAsync(TimeSpan.FromSeconds(5))).Subject.Should().Be("later");
    }

    [Fact(DisplayName = "Dequeue cancelado lança OperationCanceledException")]
    public async Task Dequeue_Cancelled_ShouldThrowOperationCanceled()
    {
        var sut = new ChannelEmailQueue();
        using var cts = new CancellationTokenSource();

        var pending = sut.DequeueAsync(cts.Token).AsTask();
        cts.Cancel();

        await pending.Invoking(p => p).Should().ThrowAsync<OperationCanceledException>();
    }
}
