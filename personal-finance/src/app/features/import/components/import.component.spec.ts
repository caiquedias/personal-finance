import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { RouterModule } from '@angular/router';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ImportComponent } from './import.component';

// Cria um File falso com nome e tamanho específicos
function makeFile(name: string, sizeBytes = 1024): File {
  const content = new Uint8Array(sizeBytes);
  return new File([content], name, { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
}

describe('ImportComponent', () => {
  let fixture: ComponentFixture<ImportComponent>;
  let component: ImportComponent;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ImportComponent, RouterModule.forRoot([])],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture   = TestBed.createComponent(ImportComponent);
    component = fixture.componentInstance;
    httpMock  = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('cria o componente', () => {
    expect(component).toBeTruthy();
  });

  describe('onDragOver()', () => {
    it('define isDragging=true e previne comportamento padrão', () => {
      const event = new DragEvent('dragover');
      spyOn(event, 'preventDefault');
      component.onDragOver(event);
      expect(component.isDragging()).toBeTrue();
      expect(event.preventDefault).toHaveBeenCalled();
    });
  });

  describe('onDrop()', () => {
    it('define isDragging=false', () => {
      component.isDragging.set(true);
      const event = new DragEvent('drop');
      spyOn(event, 'preventDefault');
      component.onDrop(event);
      expect(component.isDragging()).toBeFalse();
    });

    it('rejeita arquivo não-xlsx e define estado de erro', () => {
      const file = makeFile('planilha.csv');
      const event = new DragEvent('drop', {
        dataTransfer: new DataTransfer(),
      });
      Object.defineProperty(event, 'dataTransfer', {
        value: { files: [file] },
      });
      spyOn(event, 'preventDefault');
      component.onDrop(event);
      expect(component.state()).toBe('error');
      expect(component.errorMessage()).toContain('.xlsx');
    });
  });

  describe('onFileSelected()', () => {
    it('define arquivo selecionado para xlsx válido', () => {
      const file = makeFile('planilha.xlsx');
      const event = { target: { files: [file] } } as unknown as Event;
      component.onFileSelected(event);
      expect(component.selectedFile()).toBe(file);
      expect(component.state()).toBe('idle');
    });

    it('rejeita arquivo maior que 10 MB', () => {
      const file = makeFile('grande.xlsx', 11 * 1024 * 1024);
      const event = { target: { files: [file] } } as unknown as Event;
      component.onFileSelected(event);
      expect(component.state()).toBe('error');
      expect(component.errorMessage()).toContain('10 MB');
    });

    it('rejeita arquivo com extensão errada', () => {
      const file = makeFile('dados.csv');
      const event = { target: { files: [file] } } as unknown as Event;
      component.onFileSelected(event);
      expect(component.state()).toBe('error');
      expect(component.errorMessage()).toContain('.xlsx');
    });
  });

  describe('resetState()', () => {
    it('reseta todos os signals para estado inicial', () => {
      component.state.set('done');
      component.selectedFile.set(makeFile('x.xlsx'));
      component.uploadProgress.set(50);
      component.errorMessage.set('erro');

      component.resetState();

      expect(component.state()).toBe('idle');
      expect(component.selectedFile()).toBeNull();
      expect(component.uploadProgress()).toBe(0);
      expect(component.errorMessage()).toBeNull();
    });
  });

  describe('formatSize()', () => {
    it('retorna bytes para valores < 1 KB', () => {
      expect(component.formatSize(512)).toBe('512 B');
    });

    it('retorna KB para valores entre 1 KB e 1 MB', () => {
      expect(component.formatSize(2048)).toBe('2.0 KB');
    });

    it('retorna MB para valores >= 1 MB', () => {
      expect(component.formatSize(2 * 1024 * 1024)).toBe('2.0 MB');
    });
  });

  describe('clearFile()', () => {
    it('limpa arquivo selecionado e reseta input', () => {
      component.selectedFile.set(makeFile('test.xlsx'));
      const inputEl = { value: 'some-path' } as HTMLInputElement;
      component.clearFile(inputEl);
      expect(component.selectedFile()).toBeNull();
      expect(inputEl.value).toBe('');
    });
  });
});
