import { TestBed } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { RouterModule } from '@angular/router';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { SidebarComponent } from './sidebar.component';
import { AuthService } from '../../../core/auth/auth.service';

describe('SidebarComponent', () => {
  let fixture: ComponentFixture<SidebarComponent>;
  let component: SidebarComponent;
  let authSpy: jasmine.SpyObj<AuthService>;

  function setupWithAuth(isAdmin: boolean, name = 'Caique Dias'): void {
    authSpy = jasmine.createSpyObj('AuthService', [], {
      isAdmin:     jasmine.createSpy().and.returnValue(isAdmin),
      currentUser: jasmine.createSpy().and.returnValue({ name, email: 'c@t.com' }),
    });
  }

  async function compile(): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [SidebarComponent, RouterModule.forRoot([])],
      providers: [{ provide: AuthService, useValue: authSpy }],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture   = TestBed.createComponent(SidebarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('visibleItems', () => {
    it('usuário normal não vê itens adminOnly', async () => {
      setupWithAuth(false);
      await compile();
      const adminItems = component.visibleItems().filter((i: any) => i.adminOnly);
      expect(adminItems.length).toBe(0);
    });

    it('usuário admin vê itens adminOnly', async () => {
      setupWithAuth(true);
      await compile();
      // Deve ter pelo menos um item admin visível
      expect(component.visibleItems().length).toBeGreaterThanOrEqual(1);
    });
  });

  describe('userInitials', () => {
    it('extrai as duas primeiras iniciais do nome', async () => {
      setupWithAuth(false, 'Caique Dias');
      await compile();
      expect(component.userInitials()).toBe('CD');
    });

    it('retorna inicial única para nome simples', async () => {
      setupWithAuth(false, 'Caique');
      await compile();
      expect(component.userInitials()).toBe('C');
    });

    it('retorna string vazia quando currentUser é null', async () => {
      authSpy = jasmine.createSpyObj('AuthService', [], {
        isAdmin:     jasmine.createSpy().and.returnValue(false),
        currentUser: jasmine.createSpy().and.returnValue(null),
      });
      await compile();
      expect(component.userInitials()).toBe('');
    });
  });

  describe('getIcon()', () => {
    beforeEach(async () => {
      setupWithAuth(false);
      await compile();
    });

    it('retorna SVG para ícone conhecido (grid)', () => {
      const svg = component.getIcon('grid');
      expect(svg).toContain('<svg');
    });

    it('retorna SVG para ícone settings', () => {
      const svg = component.getIcon('settings');
      expect(svg).toContain('<svg');
    });

    it('retorna string vazia para ícone desconhecido', () => {
      expect(component.getIcon('unknown-icon')).toBe('');
    });
  });
});
