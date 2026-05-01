import { Component, input, output } from '@angular/core';

@Component({
  selector: 'app-filter-button',
  standalone: true,
  templateUrl: './filter-button.component.html',
  styleUrls: ['./filter-button.component.css'],
})
export class FilterButtonComponent {
  readonly activeCount = input(0);
  readonly toggled = output<void>();
}
