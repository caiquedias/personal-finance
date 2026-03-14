using FluentAssertions;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using Xunit;

namespace PersonalFinance.Domain.Tests.Unit.Financial;

// ══════════════════════════════════════════════════════════════════════════════
// PERIOD
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Testes da entidade Period (período mensal de referência).
/// Regra: um único período por usuário/mês/ano.
/// </summary>
public class PeriodTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    // ── Criação válida ────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve criar período com dados válidos")]
    public void Create_WithValidData_ShouldSucceed()
    {
        var period = Period.Create(UserId, 2026, 4);

        period.UserId.Should().Be(UserId);
        period.Year.Should().Be(2026);
        period.Month.Should().Be(4);
        period.IsActive.Should().BeTrue();
    }

    // ── Validações de mês ─────────────────────────────────────────────────────

    [Theory(DisplayName = "Deve lançar exceção para mês inválido")]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    [InlineData(99)]
    public void Create_WithInvalidMonth_ShouldThrow(int month)
    {
        var act = () => Period.Create(UserId, 2026, month);
        act.Should().Throw<DomainException>()
           .WithMessage("*mês*");
    }

    [Theory(DisplayName = "Deve aceitar todos os meses válidos (1–12)")]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    public void Create_WithValidMonth_ShouldSucceed(int month)
    {
        var act = () => Period.Create(UserId, 2026, month);
        act.Should().NotThrow();
    }

    // ── Validações de ano ─────────────────────────────────────────────────────

    [Theory(DisplayName = "Deve lançar exceção para ano inválido")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1899)]
    public void Create_WithInvalidYear_ShouldThrow(int year)
    {
        var act = () => Period.Create(UserId, year, 1);
        act.Should().Throw<DomainException>()
           .WithMessage("*ano*");
    }

    [Fact(DisplayName = "Deve lançar exceção para UserId vazio")]
    public void Create_WithEmptyUserId_ShouldThrow()
    {
        var act = () => Period.Create(Guid.Empty, 2026, 1);
        act.Should().Throw<DomainException>();
    }

    // ── Soft delete ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "SoftDelete deve desativar o período")]
    public void SoftDelete_ShouldDeactivatePeriod()
    {
        var period = Period.Create(UserId, 2026, 4);
        period.SoftDelete();

        period.IsActive.Should().BeFalse();
        period.IsDeleted.Should().BeTrue();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// EXPENSE
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Testes da entidade Expense (despesa).
/// Regras: Amount > 0, DueDate obrigatória, PaymentDate só quando Paid.
/// </summary>
public class ExpenseTests
{
    private static readonly Guid UserId     = Guid.NewGuid();
    private static readonly Guid PeriodId   = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    private static Expense CreateValid() => Expense.Create(
        periodId:        PeriodId,
        userId:          UserId,
        categoryId:      CategoryId,
        sourceType:      SourceType.Personal,
        fortnightType:   FortnightType.First,
        paymentStatus:   PaymentStatus.Pending,
        description:     "Aluguel",
        amount:          1200.00m,
        dueDate:         new DateOnly(2026, 4, 10),
        paymentDate:     null,
        notes:           null
    );

    // ── Criação válida ────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve criar despesa com dados válidos")]
    public void Create_WithValidData_ShouldSucceed()
    {
        var expense = CreateValid();

        expense.PeriodId.Should().Be(PeriodId);
        expense.UserId.Should().Be(UserId);
        expense.CategoryId.Should().Be(CategoryId);
        expense.SourceType.Should().Be(SourceType.Personal);
        expense.FortnightType.Should().Be(FortnightType.First);
        expense.PaymentStatus.Should().Be(PaymentStatus.Pending);
        expense.Description.Should().Be("Aluguel");
        expense.Amount.Should().Be(1200.00m);
        expense.DueDate.Should().Be(new DateOnly(2026, 4, 10));
        expense.PaymentDate.Should().BeNull();
        expense.Notes.Should().BeNull();
        expense.IsActive.Should().BeTrue();
    }

    // ── Validações de valor ───────────────────────────────────────────────────

    [Theory(DisplayName = "Deve lançar exceção para Amount inválido")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Create_WithInvalidAmount_ShouldThrow(double amount)
    {
        var act = () => Expense.Create(PeriodId, UserId, CategoryId,
            SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
            "Teste", (decimal)amount, new DateOnly(2026, 4, 10), null, null);

        act.Should().Throw<DomainException>()
           .WithMessage("*valor*");
    }

    [Fact(DisplayName = "Deve aceitar Amount com centavos")]
    public void Create_WithDecimalAmount_ShouldSucceed()
    {
        var act = () => Expense.Create(PeriodId, UserId, CategoryId,
            SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
            "Teste", 0.01m, new DateOnly(2026, 4, 10), null, null);

        act.Should().NotThrow();
    }

    // ── Validações de descrição ───────────────────────────────────────────────

    [Theory(DisplayName = "Deve lançar exceção para descrição inválida")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidDescription_ShouldThrow(string? description)
    {
        var act = () => Expense.Create(PeriodId, UserId, CategoryId,
            SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
            description!, 100m, new DateOnly(2026, 4, 10), null, null);

        act.Should().Throw<DomainException>()
           .WithMessage("*descrição*");
    }

    [Fact(DisplayName = "Deve lançar exceção para descrição com mais de 200 caracteres")]
    public void Create_WithDescriptionTooLong_ShouldThrow()
    {
        var description = new string('A', 201);
        var act = () => Expense.Create(PeriodId, UserId, CategoryId,
            SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
            description, 100m, new DateOnly(2026, 4, 10), null, null);

        act.Should().Throw<DomainException>();
    }

    // ── Validações de IDs ─────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve lançar exceção para PeriodId vazio")]
    public void Create_WithEmptyPeriodId_ShouldThrow()
    {
        var act = () => Expense.Create(Guid.Empty, UserId, CategoryId,
            SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
            "Teste", 100m, new DateOnly(2026, 4, 10), null, null);

        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "Deve lançar exceção para CategoryId vazio")]
    public void Create_WithEmptyCategoryId_ShouldThrow()
    {
        var act = () => Expense.Create(PeriodId, UserId, Guid.Empty,
            SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
            "Teste", 100m, new DateOnly(2026, 4, 10), null, null);

        act.Should().Throw<DomainException>();
    }

    // ── PaymentDate ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "PaymentDate deve ser nulo quando status é Pending")]
    public void Create_PendingStatus_PaymentDateShouldBeNull()
    {
        var expense = CreateValid(); // status = Pending
        expense.PaymentDate.Should().BeNull();
    }

    [Fact(DisplayName = "MarkAsPaid deve definir status Paid e PaymentDate")]
    public void MarkAsPaid_ShouldSetStatusAndPaymentDate()
    {
        var expense     = CreateValid();
        var paymentDate = new DateOnly(2026, 3, 9);

        expense.MarkAsPaid(paymentDate);

        expense.PaymentStatus.Should().Be(PaymentStatus.Paid);
        expense.PaymentDate.Should().Be(paymentDate);
    }

    [Fact(DisplayName = "MarkAsPaid com data futura deve lançar exceção")]
    public void MarkAsPaid_WithFutureDate_ShouldThrow()
    {
        var expense     = CreateValid();
        var futureDate  = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

        var act = () => expense.MarkAsPaid(futureDate);
        act.Should().Throw<DomainException>()
           .WithMessage("*data de pagamento*");
    }

    [Fact(DisplayName = "MarkAsCancelled deve definir status Cancelled")]
    public void MarkAsCancelled_ShouldSetCancelledStatus()
    {
        var expense = CreateValid();
        expense.MarkAsCancelled();

        expense.PaymentStatus.Should().Be(PaymentStatus.Cancelled);
        expense.PaymentDate.Should().BeNull();
    }

    [Fact(DisplayName = "MarkAsPartial deve definir status Partial")]
    public void MarkAsPartial_ShouldSetPartialStatus()
    {
        var expense = CreateValid();
        expense.MarkAsPartial();
        expense.PaymentStatus.Should().Be(PaymentStatus.Partial);
    }

    // ── Atualização ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "Update deve alterar os campos e atualizar UpdatedAt")]
    public void Update_ShouldChangeFieldsAndTimestamp()
    {
        var expense  = CreateValid();
        var original = expense.UpdatedAt;

        Task.Delay(10).Wait();
        expense.Update(
            categoryId:    CategoryId,
            sourceType:    SourceType.Parental,
            fortnightType: FortnightType.Second,
            description:   "Internet",
            amount:        150m,
            dueDate:       new DateOnly(2026, 4, 20),
            notes:         "Vence dia 20"
        );

        expense.Description.Should().Be("Internet");
        expense.Amount.Should().Be(150m);
        expense.FortnightType.Should().Be(FortnightType.Second);
        expense.SourceType.Should().Be(SourceType.Parental);
        expense.Notes.Should().Be("Vence dia 20");
        expense.UpdatedAt.Should().BeAfter(original);
    }

    // ── Soft delete ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "SoftDelete deve desativar a despesa")]
    public void SoftDelete_ShouldDeactivateExpense()
    {
        var expense = CreateValid();
        expense.SoftDelete();

        expense.IsActive.Should().BeFalse();
        expense.IsDeleted.Should().BeTrue();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// INCOME
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Testes da entidade Income (receita).
/// </summary>
public class IncomeTests
{
    private static readonly Guid UserId   = Guid.NewGuid();
    private static readonly Guid PeriodId = Guid.NewGuid();

    private static Income CreateValid() => Income.Create(
        periodId:      PeriodId,
        userId:        UserId,
        fortnightType: FortnightType.First,
        description:   "Adiantamento MDS",
        amount:        5500.00m,
        receivedAt:    new DateOnly(2026, 4, 15),
        notes:         null
    );

    // ── Criação válida ────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve criar receita com dados válidos")]
    public void Create_WithValidData_ShouldSucceed()
    {
        var income = CreateValid();

        income.PeriodId.Should().Be(PeriodId);
        income.UserId.Should().Be(UserId);
        income.FortnightType.Should().Be(FortnightType.First);
        income.Description.Should().Be("Adiantamento MDS");
        income.Amount.Should().Be(5500.00m);
        income.ReceivedAt.Should().Be(new DateOnly(2026, 4, 15));
        income.Notes.Should().BeNull();
        income.IsActive.Should().BeTrue();
    }

    // ── Validações de valor ───────────────────────────────────────────────────

    [Theory(DisplayName = "Deve lançar exceção para Amount inválido")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithInvalidAmount_ShouldThrow(double amount)
    {
        var act = () => Income.Create(PeriodId, UserId, FortnightType.First,
            "Teste", (decimal)amount, new DateOnly(2026, 4, 15), null);

        act.Should().Throw<DomainException>()
           .WithMessage("*valor*");
    }

    // ── Validações de descrição ───────────────────────────────────────────────

    [Theory(DisplayName = "Deve lançar exceção para descrição inválida")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidDescription_ShouldThrow(string? description)
    {
        var act = () => Income.Create(PeriodId, UserId, FortnightType.First,
            description!, 5500m, new DateOnly(2026, 4, 15), null);

        act.Should().Throw<DomainException>()
           .WithMessage("*descrição*");
    }

    // ── Validações de IDs ─────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve lançar exceção para PeriodId vazio")]
    public void Create_WithEmptyPeriodId_ShouldThrow()
    {
        var act = () => Income.Create(Guid.Empty, UserId, FortnightType.First,
            "Teste", 100m, new DateOnly(2026, 4, 15), null);

        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "Deve lançar exceção para UserId vazio")]
    public void Create_WithEmptyUserId_ShouldThrow()
    {
        var act = () => Income.Create(PeriodId, Guid.Empty, FortnightType.First,
            "Teste", 100m, new DateOnly(2026, 4, 15), null);

        act.Should().Throw<DomainException>();
    }

    // ── Atualização ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "Update deve alterar campos e atualizar UpdatedAt")]
    public void Update_ShouldChangeFieldsAndTimestamp()
    {
        var income   = CreateValid();
        var original = income.UpdatedAt;

        Task.Delay(10).Wait();
        income.Update(FortnightType.Second, "Saldo MDS", 2613.15m,
                      new DateOnly(2026, 4, 30), "Saldo do mês");

        income.FortnightType.Should().Be(FortnightType.Second);
        income.Description.Should().Be("Saldo MDS");
        income.Amount.Should().Be(2613.15m);
        income.Notes.Should().Be("Saldo do mês");
        income.UpdatedAt.Should().BeAfter(original);
    }

    // ── Soft delete ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "SoftDelete deve desativar a receita")]
    public void SoftDelete_ShouldDeactivateIncome()
    {
        var income = CreateValid();
        income.SoftDelete();

        income.IsActive.Should().BeFalse();
        income.IsDeleted.Should().BeTrue();
    }
}
