namespace PersonalFinance.Application.DTOs.Auth;

/// <summary>Resposta genérica (anti-enumeração): só uma mensagem, idêntica para qualquer cenário.</summary>
public sealed record GenericMessageResponseDto(string Message);
