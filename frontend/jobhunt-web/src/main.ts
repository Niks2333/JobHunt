import { bootstrapApplication } from '@angular/platform-browser';
import { applicationConfig } from './app/app.config';
import { ApplicationComponent } from './app/app.component';

bootstrapApplication(ApplicationComponent, applicationConfig).catch((applicationError) =>
  console.error(applicationError),
);
