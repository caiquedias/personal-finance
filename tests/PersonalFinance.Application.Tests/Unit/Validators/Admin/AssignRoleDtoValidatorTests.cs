using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Admin;

public class AssignRoleDtoValidatorTests
{
    private readonly IValidator<AssignRoleDto> _sut = ValidatorLocator.Get<AssignRoleDto>();

    private static string Str(int n) => new('a', n);
    private static string Email(int total) => new string('a', total - "@x.com".Length) + "@x.com";

    private static AssignRoleDto Dto(Guid? id = null, int roleId = 2) => new(id ?? Guid.NewGuid(), roleId);

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Fact] public void UserId_Empty_ShouldFail() => _sut.Validate(Dto(Guid.Empty)).Errors.Should().Contain(e => e.PropertyName == "UserId");

    [Theory, InlineData(0), InlineData(-1)]
    public void RoleId_NotPositive_ShouldFail(int v) => _sut.Validate(Dto(roleId: v)).Errors.Should().Contain(e => e.PropertyName == "RoleId");

    [Fact] public void RoleId_1_ShouldPass() => _sut.Validate(Dto(roleId: 1)).IsValid.Should().BeTrue();
}
