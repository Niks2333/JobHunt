import { Component, ElementRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { JobApplicationService } from '../../services/job-application.service';
import { JobApplicationResponse } from '../../models/job-application-response.model';
import { CreateJobApplicationRequest } from '../../models/create-job-application-request.model';
import { JobApplicationFormComponent } from '../../components/job-application-form/job-application-form.component';

@Component({
  selector: 'app-job-application-list',
  imports: [DatePipe, RouterLink, JobApplicationFormComponent],
  templateUrl: './job-application-list.component.html',
  styleUrl: './job-application-list.component.css',
})
export class JobApplicationListComponent implements OnInit {
  private readonly jobApplicationService = inject(JobApplicationService);
  private readonly addApplicationButton =
    viewChild.required<ElementRef<HTMLButtonElement>>('addApplicationButton');
  readonly jobApplications = signal<JobApplicationResponse[]>([]);
  readonly isLoading = signal(true);
  readonly loadError = signal('');
  readonly isFormOpen = signal(false);
  readonly isSaving = signal(false);
  readonly saveError = signal('');
  readonly successMessage = signal('');

  ngOnInit() {
    this.loadJobApplications();
  }

  loadJobApplications() {
    this.isLoading.set(true);
    this.loadError.set('');
    this.jobApplicationService.getJobApplications().subscribe({
      next: (jobApplications) => {
        this.jobApplications.set(jobApplications);
        this.isLoading.set(false);
      },
      error: () => {
        this.loadError.set(
          'We could not load your applications. Check that the backend is running and try again.',
        );
        this.isLoading.set(false);
      },
    });
  }

  openApplicationForm() {
    this.saveError.set('');
    this.successMessage.set('');
    this.isFormOpen.set(true);
  }

  closeApplicationForm() {
    if (this.isSaving()) return;
    this.isFormOpen.set(false);
    this.addApplicationButton().nativeElement.focus();
  }

  saveJobApplication(request: CreateJobApplicationRequest) {
    if (this.isSaving()) return;
    this.isSaving.set(true);
    this.saveError.set('');
    this.jobApplicationService.createJobApplication(request).subscribe({
      next: (savedApplication) => {
        this.isSaving.set(false);
        this.closeApplicationForm();
        this.successMessage.set(`Application to ${savedApplication.companyName} saved.`);
        this.loadJobApplications();
      },
      error: (httpError: HttpErrorResponse) => {
        const validationErrors = httpError.error?.errors as Record<string, string[]> | undefined;
        this.saveError.set(
          validationErrors
            ? Object.values(validationErrors).flat().join(' ')
            : 'Your application could not be saved. Your entries are still here; please try again.',
        );
        this.isSaving.set(false);
      },
    });
  }
}
