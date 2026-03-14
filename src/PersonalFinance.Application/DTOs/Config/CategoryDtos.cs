namespace PersonalFinance.Application.DTOs.Config;

/// <summary>Dados para criação de categoria.</summary>
public sealed record CreateCategoryDto(
    string  Name,
    string  Color,
    string? Icon,
    Guid?   UserId,
    bool    IsGlobal = false
);

/// <summary>Dados para atualização de categoria.</summary>
public sealed record UpdateCategoryDto(
    Guid    Id,
    Guid    UserId,
    string  Name,
    string  Color,
    string? Icon
);

/// <summary>Retorno de operações com categoria.</summary>
public sealed record CategoryResponseDto(
    Guid    Id,
    Guid?   UserId,
    string  Name,
    string  Color,
    string? Icon,
    bool    IsGlobal,
    bool    IsActive
);
