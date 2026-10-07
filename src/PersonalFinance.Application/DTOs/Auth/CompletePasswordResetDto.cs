namespace PersonalFinance.Application.DTOs.Auth;

/// <summary>Conclusão do reset de senha: e-mail, código de 6 dígitos e nova senha.</summary>
public sealed record CompletePasswordResetDto(string Email, string Code, string NewPassword);
