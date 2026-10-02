namespace PersonalFinance.Application.Options
{
    /// <summary>
    /// Parâmetros do MFA/TOTP (seção Auth:Mfa).
    /// </summary>
    public sealed class MfaOptions
    {
        /// <summary>
        /// Feature flag: quando true, o login de usuário com MFA ativo exige o 2º fator.
        /// Default false — com false o login segue como antes, mesmo com MFA ativo.
        /// </summary>
        public bool Enforce { get; set; }

        /// <summary>Chave AES-256 (Base64 de 32 bytes) que cifra o secret TOTP em repouso. Segredo — nunca logar.</summary>
        public string? EncryptionKey { get; set; }
    }
}
