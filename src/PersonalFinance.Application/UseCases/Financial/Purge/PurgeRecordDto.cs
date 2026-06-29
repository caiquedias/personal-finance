namespace PersonalFinance.Application.UseCases.Financial.Purge;

/// <summary>
/// DTO de retorno para registros de expurgo.
/// Mapeia os campos da entidade PurgeRecord para o payload da API,
/// expondo 'year'/'month' em vez de 'periodYear'/'periodMonth'
/// e 'itemCount' como soma de ExpenseCount + IncomeCount.
/// </summary>
public sealed class PurgeRecordDto
{
    public Guid     Id           { get; init; }
    public int      Year         { get; init; }
    public int      Month        { get; init; }
    public int      ItemCount    { get; init; }
    public decimal  TotalIncome  { get; init; }
    public decimal  TotalExpense { get; init; }
    public DateTime PurgedAt     { get; init; }
}
