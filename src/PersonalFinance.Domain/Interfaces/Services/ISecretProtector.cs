namespace PersonalFinance.Domain.Interfaces.Services;

/// <summary>Cifra/decifra segredos em repouso (ex.: secret TOTP). Implementado em Infrastructure.</summary>
public interface ISecretProtector
{
    string Protect(string plain);
    string Unprotect(string value);
}
