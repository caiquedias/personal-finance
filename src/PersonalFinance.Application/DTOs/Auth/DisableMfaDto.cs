namespace PersonalFinance.Application.DTOs.Auth;

/// <summary>Desativação do MFA: senha E (código TOTP ou recovery code).</summary>
public sealed record DisableMfaDto(string Password, string Code);
