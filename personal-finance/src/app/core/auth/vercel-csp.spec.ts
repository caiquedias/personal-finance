import vercelConfig from '../../../../vercel.json';

interface VercelHeader { key: string; value: string }
interface VercelRule { source: string; headers: VercelHeader[] }

describe('vercel.json — CSP do SPA', () => {
  const rules = ((vercelConfig as { headers?: VercelRule[] }).headers ?? []);
  const headers = rules.flatMap(r => r.headers);
  const csp = headers.find(h => h.key === 'Content-Security-Policy')?.value ?? '';

  function directive(name: string): string {
    const found = csp.split(';').map(d => d.trim()).find(d => d.startsWith(name + ' ') || d === name);
    return found ?? '';
  }

  it('define headers para todas as rotas', () => {
    expect(rules.some(r => r.source === '/(.*)')).toBeTrue();
  });

  it('define Content-Security-Policy bloqueante (não Report-Only)', () => {
    expect(csp).not.toBe('');
    expect(headers.some(h => h.key === 'Content-Security-Policy-Report-Only')).toBeFalse();
  });

  it('script-src é somente self, sem unsafe-inline/unsafe-eval', () => {
    const script = directive('script-src');
    expect(script).toBe("script-src 'self'");
  });

  it('connect-src inclui self e o host do Render', () => {
    const connect = directive('connect-src');
    expect(connect).toContain("'self'");
    expect(connect).toContain('https://personal-finance-zkyj.onrender.com');
  });

  it('permite Google Fonts em style-src e font-src', () => {
    expect(directive('style-src')).toContain('https://fonts.googleapis.com');
    expect(directive('font-src')).toContain('https://fonts.gstatic.com');
  });

  it('restringe frame-ancestors, object-src e base-uri', () => {
    expect(directive('default-src')).toBe("default-src 'self'");
    expect(directive('frame-ancestors')).toBe("frame-ancestors 'none'");
    expect(directive('object-src')).toBe("object-src 'none'");
    expect(directive('base-uri')).toBe("base-uri 'self'");
  });

  it('define X-Content-Type-Options nosniff e Referrer-Policy', () => {
    expect(headers.find(h => h.key === 'X-Content-Type-Options')?.value).toBe('nosniff');
    expect(headers.find(h => h.key === 'Referrer-Policy')?.value).toBeTruthy();
  });
});
