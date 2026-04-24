import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SidebarComponent } from '../sidebar/sidebar.component';

@Component({
  selector: 'app-page-shell',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent],
  templateUrl: './page-shell.component.html',
  styleUrls: ['./page-shell.component.css']
})
export class PageShellComponent {}
