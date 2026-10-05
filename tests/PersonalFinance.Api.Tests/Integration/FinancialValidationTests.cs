using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Application.DTOs.Financial;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>Validação FluentValidation do módulo Financial (#397): DI, mapeamento 400 e contrato sem "errors".</summary>
public class FinancialValidationTests : ApiIntegrationTestBase
{
    private readonly TestWebApplicationFactory _factory;

    public FinancialValidationTests(TestWebApplicationFactory factory) : base(factory) => _factory = factory;

    private static string Str(int n) => new('a', n);

    private static async Task AssertValidationPayloadAsync(HttpResponseMessage r)
    {
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        body.TryGetProperty("status", out _).Should().BeTrue();
        body.TryGetProperty("traceId", out _).Should().BeTrue();
        body.TryGetProperty("errors", out _).Should().BeFalse("o contrato é {status,error,message,traceId}, sem ProblemDetails");
    }

    private static object ExpenseBody(string description = "Aluguel", decimal amount = 100m, string? notes = null) => new
    {
        periodId = Guid.NewGuid(), categoryId = Guid.NewGuid(), sourceType = 2, fortnightType = 1,
        description, amount, dueDate = "2026-05-10", notes
    };

    private static object IncomeBody(string description = "Salario", string? notes = null) => new
    {
        periodId = Guid.NewGuid(), fortnightType = 1, description, amount = 100m,
        receivedAt = "2026-05-05", notes
    };

    // ── DI ────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "IValidator<T> deve ser resolvível para os DTOs financeiros")]
    public void Validators_ShouldBeResolvable()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetService<IValidator<CreatePeriodDto>>().Should().NotBeNull();
        sp.GetService<IValidator<CreateExpenseDto>>().Should().NotBeNull();
        sp.GetService<IValidator<UpdateExpenseDto>>().Should().NotBeNull();
        sp.GetService<IValidator<CreateIncomeDto>>().Should().NotBeNull();
        sp.GetService<IValidator<UpdateIncomeDto>>().Should().NotBeNull();
        sp.GetService<IValidator<CreateExpensesBatchDto>>().Should().NotBeNull();
        sp.GetService<IValidator<BatchExpenseItemDto>>().Should().NotBeNull();
        sp.GetService<IValidator<ReplicateExpensesDto>>().Should().NotBeNull();
        sp.GetService<IValidator<SaveExpenseOrderDto>>().Should().NotBeNull();
        sp.GetService<IValidator<ExpenseOrderItemDto>>().Should().NotBeNull();

        var markAsPaid = typeof(CreatePeriodDto).Assembly.GetType("PersonalFinance.Application.DTOs.Financial.MarkAsPaidDto");
        markAsPaid.Should().NotBeNull("MarkAsPaidDto deve ser movido para a Application");
        sp.GetService(typeof(IValidator<>).MakeGenericType(markAsPaid!)).Should().NotBeNull();
    }

    // ── Period ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST /periods com month 13 deve retornar 400 com message")]
    public async Task CreatePeriod_WithMonth13_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        await AssertValidationPayloadAsync(await client.PostAsJsonAsync("/api/v1/periods", new { year = 2026, month = 13 }));
    }

    [Fact(DisplayName = "POST /periods com year 3000 deve retornar 400 (sem overflow)")]
    public async Task CreatePeriod_WithYear3000_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        await AssertValidationPayloadAsync(await client.PostAsJsonAsync("/api/v1/periods", new { year = 3000, month = 5 }));
    }

    // ── Expense ───────────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST /expenses com Notes de 501 caracteres deve retornar 400")]
    public async Task CreateExpense_WithHugeNotes_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        await AssertValidationPayloadAsync(await client.PostAsJsonAsync("/api/v1/expenses", ExpenseBody(notes: Str(501))));
    }

    [Fact(DisplayName = "POST /expenses com Amount de 3 casas decimais deve retornar 400")]
    public async Task CreateExpense_WithThreeDecimals_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        await AssertValidationPayloadAsync(await client.PostAsJsonAsync("/api/v1/expenses", ExpenseBody(amount: 146.527m)));
    }

    [Fact(DisplayName = "PUT /expenses/{id} com Amount 0 deve retornar 400")]
    public async Task UpdateExpense_WithZeroAmount_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        var r = await client.PutAsJsonAsync($"/api/v1/expenses/{Guid.NewGuid()}", new
        {
            categoryId = Guid.NewGuid(), sourceType = 2, fortnightType = 1, description = "x",
            amount = 0m, dueDate = "2026-05-10", status = 1
        });
        await AssertValidationPayloadAsync(r);
    }

    [Fact(DisplayName = "PATCH /expenses/{id}/pay com data default deve retornar 400 (validação antes da busca)")]
    public async Task MarkAsPaid_WithDefaultDate_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        var r = await client.PatchAsJsonAsync($"/api/v1/expenses/{Guid.NewGuid()}/pay", new { paymentDate = "0001-01-01" });
        await AssertValidationPayloadAsync(r);
    }

    [Fact(DisplayName = "PATCH /expenses/{id}/pay com data futura deve retornar 400")]
    public async Task MarkAsPaid_WithFutureDate_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        var future = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)).ToString("yyyy-MM-dd");
        var r = await client.PatchAsJsonAsync($"/api/v1/expenses/{Guid.NewGuid()}/pay", new { paymentDate = future });
        await AssertValidationPayloadAsync(r);
    }

    [Fact(DisplayName = "POST /expenses/batch/create com item inválido deve retornar 400")]
    public async Task CreateBatch_WithInvalidItem_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        var r = await client.PostAsJsonAsync("/api/v1/expenses/batch/create", new
        {
            periodId = Guid.NewGuid(),
            items = new[]
            {
                new { categoryId = Guid.NewGuid(), sourceType = 2, fortnightType = 1, description = Str(201),
                      amount = 10m, dueDate = "2026-05-10" }
            }
        });
        await AssertValidationPayloadAsync(r);
    }

    [Fact(DisplayName = "POST /expenses/batch/create com lista vazia deve retornar 400")]
    public async Task CreateBatch_WithEmptyItems_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        var r = await client.PostAsJsonAsync("/api/v1/expenses/batch/create",
            new { periodId = Guid.NewGuid(), items = Array.Empty<object>() });
        await AssertValidationPayloadAsync(r);
    }

    [Fact(DisplayName = "POST /expenses/order com Order negativo deve retornar 400")]
    public async Task SaveOrder_WithNegativeOrder_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        var r = await client.PostAsJsonAsync("/api/v1/expenses/order",
            new[] { new { expenseId = Guid.NewGuid(), order = -1 } });
        await AssertValidationPayloadAsync(r);
    }

    // ── Replicate ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST /periods/{id}/replicate-expenses com lista vazia deve retornar 400")]
    public async Task Replicate_WithEmptyList_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        var r = await client.PostAsJsonAsync($"/api/v1/periods/{Guid.NewGuid()}/replicate-expenses", Array.Empty<Guid>());
        await AssertValidationPayloadAsync(r);
    }

    [Fact(DisplayName = "POST /periods/{id}/replicate-expenses com Guid.Empty na lista deve retornar 400")]
    public async Task Replicate_WithEmptyGuid_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        var r = await client.PostAsJsonAsync($"/api/v1/periods/{Guid.NewGuid()}/replicate-expenses", new[] { Guid.Empty });
        await AssertValidationPayloadAsync(r);
    }

    // ── Income ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST /incomes com Description de 201 caracteres deve retornar 400")]
    public async Task CreateIncome_WithHugeDescription_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        await AssertValidationPayloadAsync(await client.PostAsJsonAsync("/api/v1/incomes", IncomeBody(description: Str(201))));
    }

    [Fact(DisplayName = "POST /incomes com Notes de 501 caracteres deve retornar 400")]
    public async Task CreateIncome_WithHugeNotes_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        await AssertValidationPayloadAsync(await client.PostAsJsonAsync("/api/v1/incomes", IncomeBody(notes: Str(501))));
    }

    [Fact(DisplayName = "PUT /incomes/{id} com Description vazia deve retornar 400")]
    public async Task UpdateIncome_WithEmptyDescription_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        var r = await client.PutAsJsonAsync($"/api/v1/incomes/{Guid.NewGuid()}", new
        {
            fortnightType = 1, description = "", amount = 100m, receivedAt = "2026-05-05"
        });
        await AssertValidationPayloadAsync(r);
    }
}
