namespace PersonalFinance.Application.DTOs.Auth;

/// <summary>Primeiro código TOTP, que comprova a configuração do app autenticador.</summary>
public sealed record EnableMfaDto(string Code);
