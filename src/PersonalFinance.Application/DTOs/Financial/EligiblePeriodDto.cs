namespace PersonalFinance.Application.DTOs.Financial;

/// <summary>
/// Representa um período elegível ao expurgo, com totais agregados de receitas, despesas e contagem de itens.
/// </summary>
public class EligiblePeriodDto
{
    public Guid    PeriodId     { get; set; }
    public int     Year         { get; set; }
    public int     Month        { get; set; }
    public decimal TotalIncome  { get; set; }
    public decimal TotalExpense { get; set; }
    public int     ItemCount    { get; set; }
}
