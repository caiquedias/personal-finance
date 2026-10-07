namespace PersonalFinance.Api.Tests.Integration;

/// <summary>Dados de um usuário com MFA ativo criado pelos testes de integração (#393).</summary>
public sealed record MfaUserContext(
    Guid UserId, string Email, string Password, string Secret, IReadOnlyList<string> RecoveryCodes,
    string EnableCode);
