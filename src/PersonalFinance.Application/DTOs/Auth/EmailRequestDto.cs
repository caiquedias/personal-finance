namespace PersonalFinance.Application.DTOs.Auth;

/// <summary>Pedido baseado apenas em e-mail (esqueci a senha / reenvio de verificação).</summary>
public sealed record EmailRequestDto(string Email);
