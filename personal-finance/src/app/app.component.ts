import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TweaksService } from './core/services/tweaks.service';
import { LoadingComponent } from './shared/components/loading/loading.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, LoadingComponent],
  template: `<router-outlet /><app-loading />`
})
export class AppComponent implements OnInit {
  private readonly tweaks = inject(TweaksService);

  ngOnInit(): void {
    this.tweaks.apply();
  }
}
