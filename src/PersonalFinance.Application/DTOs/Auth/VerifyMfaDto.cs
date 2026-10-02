namespace PersonalFinance.Application.DTOs.Auth;

/// <summary>2º fator do login: código TOTP ou recovery code.</summary>
public sealed record VerifyMfaDto(string Code);
