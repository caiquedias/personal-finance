using FluentValidation;

namespace PersonalFinance.Application.Tests.Unit.Support;

/// <summary>Validators de teste: sempre válido / sempre inválido (sem Moq, robusto a qualquer overload).</summary>
public static class TestValidators
{
    public static IValidator<T> Valid<T>() => new InlineValidator<T>();

    public static IValidator<T> Invalid<T>(string message = "Dados inválidos.")
    {
        var v = new InlineValidator<T>();
        v.RuleFor(x => x).Must(_ => false).WithMessage(message);
        return v;
    }
}
