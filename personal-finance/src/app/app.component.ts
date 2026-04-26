import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TweaksService } from './core/services/tweaks.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `<router-outlet />`
})
export class AppComponent implements OnInit {
  private readonly tweaks = inject(TweaksService);

  ngOnInit(): void {
    this.tweaks.apply();
  }
}
