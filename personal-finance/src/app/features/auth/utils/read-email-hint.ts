/**
 * Lê o e-mail sugerido para as telas de recuperação de conta: primeiro do fragmento
 * da URL (`#email=<url-encoded>`, vindo do link do e-mail) e, na falta dele, do
 * `history.state.email` (navegação interna a partir de "Esqueci minha senha").
 * O fragmento é removido da URL após a leitura para o e-mail não ficar no histórico.
 */
export function readEmailHint(): string {
  let email = '';

  const hash = window.location.hash;
  if (hash.length > 1) {
    const raw = hash.substring(1)
      .split('&')
      .find(part => part.startsWith('email='))
      ?.substring('email='.length);

    if (raw) {
      try {
        email = decodeURIComponent(raw);
      } catch (e) {
        if (!(e instanceof URIError)) throw e;
        email = ''; // fragmento malformado: usuário informa o e-mail manualmente
      }
    }

    // Preserva o state atual e remove só o fragmento
    window.history.replaceState(
      window.history.state, '', window.location.pathname + window.location.search);
  }

  if (!email) {
    const stateEmail = (window.history.state as { email?: unknown } | null)?.email;
    if (typeof stateEmail === 'string') email = stateEmail;
  }

  return email;
}
