import { TestBed } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { RouterModule } from '@angular/router';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { PageShellComponent } from './page-shell.component';
import { AuthService } from '../../../core/auth/auth.service';
import { ThemeService } from '../../../core/services/theme.service';

describe('PageShellComponent', () => {
  let fixture: ComponentFixture<PageShellComponent>;

  beforeEach(async () => {
    const authSpy = jasmine.createSpyObj('AuthService', [], {
      isAdmin:     jasmine.createSpy().and.returnValue(false),
      currentUser: jasmine.createSpy().and.returnValue(null),
    });
    const themeSpy = jasmine.createSpyObj('ThemeService', ['toggle'], {
      isDark: jasmine.createSpy().and.returnValue(false),
    });

    await TestBed.configureTestingModule({
      imports: [PageShellComponent, RouterModule.forRoot([])],
      providers: [
        { provide: AuthService,  useValue: authSpy },
        { provide: ThemeService, useValue: themeSpy },
      ],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(PageShellComponent);
    fixture.detectChanges();
  });

  it('cria o componente sem erros', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
