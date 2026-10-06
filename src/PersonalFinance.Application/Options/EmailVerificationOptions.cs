namespace PersonalFinance.Application.Options;

/// <summary>Verificação de e-mail no login (seção Auth:EmailVerification).</summary>
public sealed class EmailVerificationOptions
{
    /// <summary>Quando true, e-mail não verificado impede o login. Default false.</summary>
    public bool Enforce { get; set; }
}
