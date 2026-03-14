namespace PersonalFinance.Application.DTOs.Admin;

// ── User management ───────────────────────────────────────────────────────────

/// <summary>Retorno de listagem/consulta de usuário pelo admin.</summary>
public sealed record AdminUserResponseDto(
    Guid         Id,
    string       Name,
    string       Email,
    bool         IsActive,
    bool         IsDeleted,
    DateTime     CreatedAt,
    IEnumerable<string> Roles
);

/// <summary>Atribuição de role a usuário.</summary>
public sealed record AssignRoleDto(
    Guid UserId,
    int  RoleId
);

/// <summary>Remoção de role de usuário.</summary>
public sealed record RemoveRoleDto(
    Guid UserId,
    int  RoleId
);

/// <summary>Reset de senha pelo admin.</summary>
public sealed record ResetPasswordDto(
    Guid   UserId,
    string NewPassword
);

// ── Lookup tables ─────────────────────────────────────────────────────────────

/// <summary>Criação de novo status de pagamento personalizado.</summary>
public sealed record CreatePaymentStatusDto(
    string Name,
    string Description
);

/// <summary>Criação de novo tipo de fonte personalizado.</summary>
public sealed record CreateSourceTypeDto(
    string Name
);

/// <summary>Criação de novo tipo de quinzena personalizado.</summary>
public sealed record CreateFortnightTypeDto(
    string Name
);

/// <summary>Retorno genérico para lookup com int PK.</summary>
public sealed record LookupResponseDto(
    int    Id,
    string Name,
    string? Description = null,
    bool   IsSystemSeed = false
);
