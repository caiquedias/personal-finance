import { CurrencyBrlPipe } from './currency-brl.pipe';

describe('CurrencyBrlPipe', () => {
  const pipe = new CurrencyBrlPipe();

  it('retorna "R$ —" para null', () => {
    expect(pipe.transform(null)).toBe('R$ —');
  });

  it('retorna "R$ —" para undefined', () => {
    expect(pipe.transform(undefined)).toBe('R$ —');
  });

  it('formata valor positivo sem sinal', () => {
    const result = pipe.transform(1000);
    expect(result).toContain('1.000');
    expect(result).not.toMatch(/^\+/);
  });

  it('formata valor negativo sem mostrar o sinal (usa abs)', () => {
    const result = pipe.transform(-500);
    expect(result).not.toMatch(/^-/);
    expect(result).toContain('500');
  });

  it('formata zero sem sinal', () => {
    const result = pipe.transform(0);
    expect(result).not.toMatch(/^\+/);
    expect(result).not.toMatch(/^-/);
  });

  describe('showSign = true', () => {
    it('valor positivo recebe prefixo +', () => {
      const result = pipe.transform(100, true);
      expect(result.startsWith('+')).toBeTrue();
    });

    it('valor negativo recebe prefixo -', () => {
      const result = pipe.transform(-100, true);
      expect(result.startsWith('-')).toBeTrue();
    });

    it('zero não recebe prefixo de sinal', () => {
      const result = pipe.transform(0, true);
      expect(result.startsWith('+')).toBeFalse();
      expect(result.startsWith('-')).toBeFalse();
    });
  });
});
