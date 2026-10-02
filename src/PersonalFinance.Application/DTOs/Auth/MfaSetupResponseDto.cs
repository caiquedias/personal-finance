namespace PersonalFinance.Application.DTOs.Auth;

/// <summary>Secret TOTP em claro (exibido uma única vez) e URI otpauth para o app autenticador.</summary>
public sealed record MfaSetupResponseDto(string Secret, string OtpAuthUri);
