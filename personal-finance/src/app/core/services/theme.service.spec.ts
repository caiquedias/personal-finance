import { TestBed } from '@angular/core/testing';
import { ThemeService, MATCH_MEDIA_FN } from './theme.service';

const THEME_KEY = 'pf_theme';

describe('ThemeService', () => {
  afterEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('dark');
    TestBed.resetTestingModule();
  });

  function createService(): ThemeService {
    TestBed.configureTestingModule({ providers: [ThemeService] });
    const svc = TestBed.inject(ThemeService);
    TestBed.flushEffects();
    return svc;
  }

  describe('inicialização', () => {
    it('usa "dark" do localStorage quando presente', () => {
      localStorage.setItem(THEME_KEY, 'dark');
      const svc = createService();
      expect(svc.isDark()).toBeTrue();
    });

    it('usa "light" do localStorage quando presente', () => {
      localStorage.setItem(THEME_KEY, 'light');
      const svc = createService();
      expect(svc.isDark()).toBeFalse();
    });

    it('usa preferência do sistema quando localStorage está vazio', () => {
      const mql = {
        matches: true, media: '', onchange: null,
        addListener: () => {}, removeListener: () => {},
        addEventListener: () => {}, removeEventListener: () => {},
        dispatchEvent: () => false,
      } as MediaQueryList;
      const matchMediaSpy = jasmine.createSpy('matchMedia').and.returnValue(mql);
      TestBed.configureTestingModule({ providers: [ThemeService] });
      TestBed.overrideProvider(MATCH_MEDIA_FN, { useValue: matchMediaSpy });
      const svc = TestBed.inject(ThemeService);
      TestBed.flushEffects();
      expect(svc.isDark()).toBeTrue();
    });

    it('aplica classe "dark" no <html> quando isDark é true', () => {
      localStorage.setItem(THEME_KEY, 'dark');
      createService();
      expect(document.documentElement.classList.contains('dark')).toBeTrue();
    });

    it('remove classe "dark" do <html> quando isDark é false', () => {
      document.documentElement.classList.add('dark');
      localStorage.setItem(THEME_KEY, 'light');
      createService();
      expect(document.documentElement.classList.contains('dark')).toBeFalse();
    });
  });

  describe('toggle()', () => {
    it('inverte isDark de false para true', () => {
      localStorage.setItem(THEME_KEY, 'light');
      const svc = createService();
      svc.toggle();
      expect(svc.isDark()).toBeTrue();
    });

    it('inverte isDark de true para false', () => {
      localStorage.setItem(THEME_KEY, 'dark');
      const svc = createService();
      svc.toggle();
      expect(svc.isDark()).toBeFalse();
    });

    it('persiste no localStorage após toggle', () => {
      localStorage.setItem(THEME_KEY, 'light');
      const svc = createService();
      svc.toggle();
      TestBed.flushEffects();
      expect(localStorage.getItem(THEME_KEY)).toBe('dark');
    });
  });
});
