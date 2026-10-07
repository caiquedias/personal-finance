using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Financial;

/// <summary>
/// MarkAsPaidDto ainda vive no controller (Api); será movido para Application/DTOs/Financial.
/// O tipo é resolvido por reflexão para o Red falhar em runtime (e não em compilação).
/// </summary>
public class MarkAsPaidDtoValidatorTests
{
    private const string DtoTypeName = "PersonalFinance.Application.DTOs.Financial.MarkAsPaidDto";

    private static readonly Type? DtoType =
        typeof(PersonalFinance.Application.UseCases.Auth.RegisterUserUseCase).Assembly.GetType(DtoTypeName);

    private static IValidator Sut()
    {
        Assert.True(DtoType is not null, $"{DtoTypeName} não encontrado no assembly da Application.");
        var type = DtoType!.Assembly.GetTypes().FirstOrDefault(t =>
            t is { IsAbstract: false, IsClass: true }
            && typeof(IValidator<>).MakeGenericType(DtoType).IsAssignableFrom(t));
        Assert.True(type is not null, "MarkAsPaidDtoValidator não encontrado.");
        return (IValidator)Activator.CreateInstance(type!)!;
    }

    private static bool IsValid(DateOnly date)
    {
        var sut = Sut();
        var dto = Activator.CreateInstance(DtoType!, date)!;
        return sut.Validate(new ValidationContext<object>(dto)).IsValid;
    }

    [Fact] public void Yesterday_ShouldPass() => IsValid(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))).Should().BeTrue();
    [Fact] public void Today_ShouldPass() => IsValid(DateOnly.FromDateTime(DateTime.UtcNow)).Should().BeTrue();
    [Fact] public void Default_ShouldFail() => IsValid(default).Should().BeFalse();
    [Fact] public void Future_ShouldFail() => IsValid(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2))).Should().BeFalse();
}
