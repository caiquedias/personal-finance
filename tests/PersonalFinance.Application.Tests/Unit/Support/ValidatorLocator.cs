using FluentValidation;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Support;

/// <summary>
/// Localiza por reflexão o validator de um DTO no assembly da Application.
/// Evita erro de compilação na fase Red (a classe ainda não existe): a ausência vira falha de asserção.
/// </summary>
public static class ValidatorLocator
{
    public static IValidator<T> Get<T>()
    {
        var type = typeof(T).Assembly.GetTypes()
            .FirstOrDefault(t => t is { IsAbstract: false, IsClass: true }
                                 && typeof(IValidator<T>).IsAssignableFrom(t));

        Assert.True(type is not null, $"Nenhum IValidator<{typeof(T).Name}> encontrado no assembly da Application.");
        return (IValidator<T>)Activator.CreateInstance(type!)!;
    }
}
