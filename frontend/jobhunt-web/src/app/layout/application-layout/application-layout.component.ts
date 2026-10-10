import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-application-layout',
  imports: [RouterLink, RouterOutlet],
  templateUrl: './application-layout.component.html',
  styleUrl: './application-layout.component.css',
})
export class ApplicationLayoutComponent {}
