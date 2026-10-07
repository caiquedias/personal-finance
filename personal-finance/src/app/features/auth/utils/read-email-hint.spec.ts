import { readEmailHint } from './read-email-hint';

describe('readEmailHint', () => {
  const base = () => location.pathname + location.search;

  function prepare(state: object | null, hash = ''): void {
    history.replaceState(state, '', base() + hash);
  }

  afterEach(() => history.replaceState(null, '', base()));

  it('lê o e-mail do fragmento #email= (decodificado)', () => {
    prepare(null, '#email=a%2Bb%40x.com');

    expect(readEmailHint()).toBe('a+b@x.com');
  });

  it('encontra email= entre outros parâmetros do fragmento', () => {
    prepare(null, '#foo=1&email=ana%40x.com');

    expect(readEmailHint()).toBe('ana@x.com');
  });

  it('fragmento malformado (URIError): retorna vazio sem lançar', () => {
    prepare(null, '#email=%E0%A4%A');

    expect(() => readEmailHint()).not.toThrow();
    expect(readEmailHint()).toBe('');
  });

  it('fragmento malformado: cai no history.state.email', () => {
    prepare({ email: 'state@x.com' }, '#email=%E0%A4%A');

    expect(readEmailHint()).toBe('state@x.com');
  });

  it('sem fragmento: usa history.state.email', () => {
    prepare({ email: 'state@x.com' });

    expect(readEmailHint()).toBe('state@x.com');
  });

  it('fragmento tem precedência sobre history.state.email', () => {
    prepare({ email: 'state@x.com' }, '#email=frag%40x.com');

    expect(readEmailHint()).toBe('frag@x.com');
  });

  it('state.email não-string é ignorado', () => {
    prepare({ email: 123 });

    expect(readEmailHint()).toBe('');
  });

  it('sem fragmento nem state: retorna vazio', () => {
    prepare(null);

    expect(readEmailHint()).toBe('');
  });

  it('remove o fragmento via replaceState preservando o state', () => {
    prepare({ notice: 'x' }, '#email=ana%40x.com');
    const spy = spyOn(history, 'replaceState').and.callThrough();

    readEmailHint();

    expect(spy).toHaveBeenCalledTimes(1);
    expect(spy.calls.mostRecent().args[0]).toEqual({ notice: 'x' });
    expect(String(spy.calls.mostRecent().args[2])).not.toContain('#');
    expect(location.hash).toBe('');
  });

  it('sem fragmento: não chama replaceState', () => {
    prepare({ email: 'a@x.com' });
    const spy = spyOn(history, 'replaceState').and.callThrough();

    readEmailHint();

    expect(spy).not.toHaveBeenCalled();
  });
});
