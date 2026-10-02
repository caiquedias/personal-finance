namespace PersonalFinance.Application.DTOs.Auth;

/// <summary>Recovery codes em claro — exibidos uma única vez; só o hash é persistido.</summary>
public sealed record EnableMfaResponseDto(IReadOnlyList<string> RecoveryCodes);
