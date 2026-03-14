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

/// <summary>Retorno do login: token JWT e dados básicos do usuário.</summary>
public sealed record LoginResponseDto(
    string Token,
    string Name,
    string Email
);

/// <summary>Retorno de criação/consulta de usuário. Nunca expõe PasswordHash.</summary>
public sealed record UserResponseDto(
    Guid   Id,
    string Name,
    string Email,
    bool   IsActive
);
