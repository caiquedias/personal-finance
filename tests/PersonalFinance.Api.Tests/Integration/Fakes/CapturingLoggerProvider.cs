using Microsoft.Extensions.Logging;

namespace PersonalFinance.Api.Tests.Integration.Fakes;

/// <summary>
/// Provider de log de teste: guarda TODA mensagem formatada (e exceção) emitida pela aplicação, para provar que
/// código/hash de reset nunca vão para os logs (#404).
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly object _lock = new();
    private readonly List<string> _lines = new();

    public string AllText
    {
        get { lock (_lock) return string.Join("\n", _lines); }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void Dispose() { }

    private void Add(string line)
    {
        lock (_lock) _lines.Add(line);
    }

    private sealed class CapturingLogger(CapturingLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => owner.Add($"[{logLevel}] {category}: {formatter(state, exception)} {exception}");
    }
}
