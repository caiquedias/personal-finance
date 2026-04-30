import { TestBed, ComponentFixture } from '@angular/core/testing';
import { SonicRingBurstComponent } from './sonic-ring-burst.component';

describe('SonicRingBurstComponent', () => {
  let fixture: ComponentFixture<SonicRingBurstComponent>;
  let component: SonicRingBurstComponent;
  let rafCallbacks: FrameRequestCallback[];
  let rafCounter: number;

  beforeEach(() => {
    rafCallbacks = [];
    rafCounter = 0;
    spyOn(window, 'requestAnimationFrame').and.callFake((cb: FrameRequestCallback) => {
      rafCallbacks.push(cb);
      return ++rafCounter;
    });
    spyOn(window, 'cancelAnimationFrame');
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SonicRingBurstComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(SonicRingBurstComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('origin', { x: 100, y: 200 });
    fixture.detectChanges();
  });

  const runFrames = (n: number) => {
    for (let i = 0; i < n; i++) {
      if (rafCallbacks.length > 0) {
        rafCallbacks.shift()!(i * 16);
      }
    }
  };

  it('spawna 24 anéis ao inicializar', () => {
    expect(component.rings().length).toBe(24);
  });

  it('emite done quando todos os anéis desaparecem', () => {
    let doneFired = false;
    component.done.subscribe(() => doneFired = true);
    runFrames(500);
    expect(doneFired).toBeTrue();
  });

  it('cancela o RAF no destroy', () => {
    component.ngOnDestroy();
    expect(window.cancelAnimationFrame).toHaveBeenCalled();
  });
});
