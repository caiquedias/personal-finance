namespace PersonalFinance.Api.Controllers.V1.Purge;

/// <summary>DTO para o body do endpoint de expurgo.</summary>
public sealed record PurgeRequestDto(string CsvFileName);
