using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Config;
using PersonalFinance.Domain.Entities.Lookup;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Config;

public class GetPaymentStatusesUseCaseTests
{
    private readonly Mock<IPaymentStatusRepository> _repository = new();
    private readonly GetPaymentStatusesUseCase      _sut;

    public GetPaymentStatusesUseCaseTests()
    {
        _sut = new GetPaymentStatusesUseCase(_repository.Object);
    }

    [Fact(DisplayName = "Deve retornar todos os status com flag IsSystemSeed correto")]
    public async Task Execute_ShouldReturnStatusesWithSeedFlag()
    {
        _repository.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new[]
        {
            new PaymentStatus { Id = 1, Name = "Pending",   Description = "Pendente" },
            new PaymentStatus { Id = 2, Name = "Paid",      Description = "Pago"     },
            new PaymentStatus { Id = 5, Name = "Customized",Description = "Personalizado" },
        });

        var result = (await _sut.ExecuteAsync()).ToList();

        result.Should().HaveCount(3);
        result.First(x => x.Id == 1).IsSystemSeed.Should().BeTrue();
        result.First(x => x.Id == 2).IsSystemSeed.Should().BeTrue();
        result.First(x => x.Id == 5).IsSystemSeed.Should().BeFalse();
    }

    [Fact(DisplayName = "Os quatro seeds fixos devem ser marcados como IsSystemSeed=true")]
    public async Task Execute_SeedIds_ShouldAllBeMarkedAsSystemSeed()
    {
        _repository.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new[]
        {
            new PaymentStatus { Id = 1, Name = "Pending",   Description = "" },
            new PaymentStatus { Id = 2, Name = "Paid",      Description = "" },
            new PaymentStatus { Id = 3, Name = "Cancelled", Description = "" },
            new PaymentStatus { Id = 4, Name = "Partial",   Description = "" },
        });

        var result = await _sut.ExecuteAsync();

        result.Should().AllSatisfy(x => x.IsSystemSeed.Should().BeTrue());
    }

    [Fact(DisplayName = "Deve retornar lista vazia se não há registros")]
    public async Task Execute_WithNoStatuses_ShouldReturnEmpty()
    {
        _repository.Setup(r => r.GetAllAsync(default))
                   .ReturnsAsync(Enumerable.Empty<PaymentStatus>());

        var result = await _sut.ExecuteAsync();

        result.Should().BeEmpty();
    }
}
