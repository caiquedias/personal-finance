using PersonalFinance.Domain.Enums;

namespace PersonalFinance.Application.Interfaces;

/// <summary>Geração e verificação de códigos de uso único (HMAC com chave do servidor).</summary>
public interface IOneTimeCodeService
{
    /// <summary>Gera código numérico de 6 dígitos.</summary>
    string GenerateCode();

    string ComputeHash(Guid tokenId, Guid userId, UserTokenPurpose purpose, string code);

    /// <summary>Compara em tempo fixo o hash do código informado com o esperado.</summary>
    bool Verify(Guid tokenId, Guid userId, UserTokenPurpose purpose, string code, string expectedHash);
}
