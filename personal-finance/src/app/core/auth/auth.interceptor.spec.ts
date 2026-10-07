import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';
import { environment } from '../../../environments/environment';

describe('authInterceptor', () => {
  const api = environment.apiUrl;
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let authSpy: jasmine.SpyObj<AuthService>;

  function setup(token: string | null): void {
    authSpy = jasmine.createSpyObj('AuthService', ['logout'], {
      token: jasmine.createSpy('token').and.returnValue(token),
    });

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authSpy },
      ],
    });

    http     = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => {
    httpMock?.verify();
    TestBed.resetTestingModule();
  });

  it('injeta cabeçalho Authorization quando há token', () => {
    setup('my-token');

    http.get(`${api}/test`).subscribe();

    const req = httpMock.expectOne(`${api}/test`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer my-token');
    req.flush({});
  });

  it('não injeta cabeçalho Authorization quando token é null', () => {
    setup(null);

    http.get(`${api}/test`).subscribe();

    const req = httpMock.expectOne(`${api}/test`);
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  it('chama logout quando resposta é 401', () => {
    setup('my-token');

    http.get(`${api}/test`).subscribe({ error: () => {} });

    const req = httpMock.expectOne(`${api}/test`);
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(authSpy.logout).toHaveBeenCalled();
  });

  it('não chama logout para outros erros HTTP', () => {
    setup('my-token');

    http.get(`${api}/test`).subscribe({ error: () => {} });

    const req = httpMock.expectOne(`${api}/test`);
    req.flush('Error', { status: 500, statusText: 'Server Error' });

    expect(authSpy.logout).not.toHaveBeenCalled();
  });

  it('não chama logout em 400 dos endpoints de recuperação (código inválido nunca é 401)', () => {
    setup(null);

    http.post(`${api}/auth/password/reset`, {}).subscribe({ error: () => {} });
    httpMock.expectOne(`${api}/auth/password/reset`)
      .flush({ message: 'Código inválido ou expirado.' }, { status: 400, statusText: 'Bad Request' });

    http.post(`${api}/auth/email/confirm`, {}).subscribe({ error: () => {} });
    httpMock.expectOne(`${api}/auth/email/confirm`)
      .flush({ message: 'Código inválido ou expirado.' }, { status: 400, statusText: 'Bad Request' });

    expect(authSpy.logout).not.toHaveBeenCalled();
  });

  it('não chama logout em 429 do rate limit de recuperação', () => {
    setup(null);

    http.post(`${api}/auth/password/forgot`, {}).subscribe({ error: () => {} });
    httpMock.expectOne(`${api}/auth/password/forgot`)
      .flush({ message: 'Tente novamente em 30 segundos.' }, { status: 429, statusText: 'Too Many Requests' });

    expect(authSpy.logout).not.toHaveBeenCalled();
  });

  it('não sobrescreve Authorization já presente (verify com challenge) mesmo com token salvo', () => {
    setup('saved-token');

    http.post(`${api}/verify`, {}, { headers: { Authorization: 'Bearer challenge' } }).subscribe();

    const req = httpMock.expectOne(`${api}/verify`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer challenge');
    req.flush({});
  });

  describe('escopo da API própria', () => {
    it('injeta Authorization na URL exata da apiUrl', () => {
      setup('my-token');

      http.get(api).subscribe();

      const req = httpMock.expectOne(api);
      expect(req.request.headers.get('Authorization')).toBe('Bearer my-token');
      req.flush({});
    });

    it('não injeta Authorization em domínio externo', () => {
      setup('my-token');

      http.get('https://evil.example.com/steal').subscribe();

      const req = httpMock.expectOne('https://evil.example.com/steal');
      expect(req.request.headers.has('Authorization')).toBeFalse();
      req.flush({});
    });

    it('não injeta Authorization em URL relativa', () => {
      setup('my-token');

      http.get('/assets/data.json').subscribe();

      const req = httpMock.expectOne('/assets/data.json');
      expect(req.request.headers.has('Authorization')).toBeFalse();
      req.flush({});
    });

    it('não injeta Authorization em host que só compartilha o prefixo da apiUrl', () => {
      setup('my-token');
      const url = `${api}x/test`;

      http.get(url).subscribe();

      const req = httpMock.expectOne(url);
      expect(req.request.headers.has('Authorization')).toBeFalse();
      req.flush({});
    });

    it('não injeta Authorization quando apiUrl é prefixo sem fronteira de path', () => {
      setup('my-token');
      const url = `${api}.evil.com/test`;

      http.get(url).subscribe();

      const req = httpMock.expectOne(url);
      expect(req.request.headers.has('Authorization')).toBeFalse();
      req.flush({});
    });

    it('não chama logout em 401 de domínio externo', () => {
      setup('my-token');

      http.get('https://thirdparty.example.com/x').subscribe({ error: () => {} });

      const req = httpMock.expectOne('https://thirdparty.example.com/x');
      req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

      expect(authSpy.logout).not.toHaveBeenCalled();
    });

    it('não chama logout em 401 de URL relativa', () => {
      setup('my-token');

      http.get('/assets/data.json').subscribe({ error: () => {} });

      const req = httpMock.expectOne('/assets/data.json');
      req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

      expect(authSpy.logout).not.toHaveBeenCalled();
    });
  });
});
