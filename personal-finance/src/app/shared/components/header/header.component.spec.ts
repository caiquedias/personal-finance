import { TestBed } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { HeaderComponent } from './header.component';
import { ThemeService } from '../../../core/services/theme.service';

describe('HeaderComponent', () => {
  let fixture: ComponentFixture<HeaderComponent>;
  let component: HeaderComponent;
  let themeSpy: jasmine.SpyObj<ThemeService>;

  beforeEach(async () => {
    themeSpy = jasmine.createSpyObj('ThemeService', ['toggle'], {
      isDark: jasmine.createSpy().and.returnValue(false),
    });

    await TestBed.configureTestingModule({
      imports: [HeaderComponent],
      providers: [{ provide: ThemeService, useValue: themeSpy }],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture   = TestBed.createComponent(HeaderComponent);
    component = fixture.componentInstance;
  });

  it('cria o componente', () => {
    expect(component).toBeTruthy();
  });

  it('injeta ThemeService', () => {
    expect(component.theme).toBeTruthy();
  });

  it('inputs title e subtitle têm valores padrão vazios', () => {
    fixture.detectChanges();
    expect(component.title()).toBe('');
    expect(component.subtitle()).toBe('');
  });

  it('inputs recebem valores corretamente', () => {
    fixture.componentRef.setInput('title', 'Dashboard');
    fixture.componentRef.setInput('subtitle', 'Visão geral');
    fixture.detectChanges();
    expect(component.title()).toBe('Dashboard');
    expect(component.subtitle()).toBe('Visão geral');
  });

  it('tweaksOpen começa como false', () => {
    fixture.detectChanges();
    expect(component.tweaksOpen()).toBeFalse();
  });

  it('clique no botão de tweaks alterna tweaksOpen', () => {
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('.header-tweaks-btn');
    btn?.click();
    expect(component.tweaksOpen()).toBeTrue();
    btn?.click();
    expect(component.tweaksOpen()).toBeFalse();
  });
});
