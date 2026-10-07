namespace PersonalFinance.Application.DTOs.Auth;

/// <summary>Confirmação de e-mail: e-mail + código de 6 dígitos.</summary>
public sealed record ConfirmEmailDto(string Email, string Code);
