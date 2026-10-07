namespace PersonalFinance.Application.DTOs.Auth;

/// <summary>Dados necessários para registrar um novo usuário.</summary>
public sealed record RegisterUserDto(
    string Name,
    string Email,
    string Password
);

/// <summary>Credenciais para autenticação.</summary>
public sealed record LoginDto(
    string Email,
    string Password
);

/// <summary>
/// Retorno do login: token JWT e dados básicos do usuário. Campos de MFA são aditivos:
/// com MfaRequired=true, Token é null e MfaToken é o token intermediário do 2º fator.
/// </summary>
public sealed record LoginResponseDto(
    string? Token,
    string Name,
    string Email,
    bool MfaRequired = false,
    string? MfaToken = null
);

/// <summary>Retorno de criação/consulta de usuário. Nunca expõe PasswordHash.</summary>
public sealed record UserResponseDto(
    Guid   Id,
    string Name,
    string Email,
    bool   IsActive
);
