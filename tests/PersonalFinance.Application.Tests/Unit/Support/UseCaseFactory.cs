namespace PersonalFinance.Application.Tests.Unit.Support;

/// <summary>
/// Constrói um use case por reflexão resolvendo cada parâmetro do construtor a partir das dependências
/// fornecidas (por atribuição de tipo). Parâmetro sem dependência correspondente falha com mensagem clara.
/// Permite testar o use case independentemente de ele já receber IValidator&lt;T&gt; no construtor.
/// </summary>
public static class UseCaseFactory
{
    public static T Create<T>(params object[] dependencies) where T : class
    {
        var ctor = typeof(T).GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var args = ctor.GetParameters().Select(p =>
            dependencies.FirstOrDefault(d => p.ParameterType.IsInstanceOfType(d))
            ?? throw new InvalidOperationException(
                $"Dependência não fornecida para {typeof(T).Name}: {p.ParameterType.Name} {p.Name}")).ToArray();
        return (T)ctor.Invoke(args);
    }
}
