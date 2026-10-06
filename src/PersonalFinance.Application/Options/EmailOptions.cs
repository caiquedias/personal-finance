namespace PersonalFinance.Application.Options;

/// <summary>Parâmetros de e-mail (seção Email).</summary>
public sealed class EmailOptions
{
    /// <summary>False: o dispatcher só loga destinatário e assunto, sem enviar.</summary>
    public bool Enabled { get; set; }
}
