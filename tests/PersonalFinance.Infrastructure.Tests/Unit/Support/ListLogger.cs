using Microsoft.Extensions.Logging;

namespace PersonalFinance.Infrastructure.Tests.Unit.Support;

/// <summary>Logger de teste que guarda nível, mensagem formatada e exceção (para asserir ausência de segredos).</summary>
public sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception), exception));

    /// <summary>Tudo que foi logado (mensagens + exceções) concatenado.</summary>
    public string AllText => string.Join("\n", Entries.Select(e => e.Message + " " + e.Exception));
}
