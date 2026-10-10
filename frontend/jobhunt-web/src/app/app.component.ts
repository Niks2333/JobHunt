import { Component } from '@angular/core';
import { ApplicationLayoutComponent } from './layout/application-layout/application-layout.component';

@Component({
  imports: [ApplicationLayoutComponent],
  selector: 'app-root',
  template: '<app-application-layout />',
})
export class ApplicationComponent {}
