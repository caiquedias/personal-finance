namespace PersonalFinance.Domain.Enums;

/// <summary>Ações administrativas sensíveis registradas na trilha de auditoria.</summary>
public enum AuditAction
{
    UserCreated = 1,
    UserUpdated = 2,
    UserActivated = 3,
    UserDeactivated = 4,
    RoleAssigned = 5,
    RoleRemoved = 6,
    PasswordReset = 7,
    MfaReset = 8,
    PasswordResetRequested = 9,
    PasswordResetCompleted = 10
}
