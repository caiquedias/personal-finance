using Microsoft.Extensions.Logging;

namespace PersonalFinance.Api.Tests.Unit.Support;

/// <summary>Logger de teste que guarda nível, mensagem formatada e exceção (para asserir ausência de segredos).</summary>
public sealed class ListLogger<T> : ILogger<T>
{
    private readonly object _lock = new();
    private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = new();

    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries
    {
        get { lock (_lock) return _entries.ToList(); }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (_lock) _entries.Add((logLevel, formatter(state, exception), exception));
    }

    /// <summary>Tudo que foi logado (mensagens + exceções) concatenado.</summary>
    public string AllText => string.Join("\n", Entries.Select(e => e.Message + " " + e.Exception));
}
