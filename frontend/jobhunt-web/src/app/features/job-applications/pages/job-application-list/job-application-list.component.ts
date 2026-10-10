import {
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, Subject, catchError, switchMap } from 'rxjs';
import { JobApplicationService } from '../../services/job-application.service';
import {
  JobApplicationResponse,
  JobApplicationStatus,
} from '../../models/job-application-response.model';
import { GetJobApplicationsRequest } from '../../models/get-job-applications-request.model';
import { CreateJobApplicationRequest } from '../../models/create-job-application-request.model';
import { JobApplicationFormComponent } from '../../components/job-application-form/job-application-form.component';

@Component({
  selector: 'app-job-application-list',
  imports: [DatePipe, RouterLink, ReactiveFormsModule, JobApplicationFormComponent],
  templateUrl: './job-application-list.component.html',
  styleUrl: './job-application-list.component.css',
})
export class JobApplicationListComponent implements OnInit {
  private readonly jobApplicationService = inject(JobApplicationService);
  private readonly destroyReference = inject(DestroyRef);
  private readonly formBuilder = inject(FormBuilder);
  private readonly loadRequests = new Subject<GetJobApplicationsRequest>();
  readonly filterForm = this.formBuilder.nonNullable.group({
    search: ['', Validators.maxLength(150)],
    status: this.formBuilder.nonNullable.control<JobApplicationStatus | ''>(''),
    source: [''],
  });
  readonly availableStatuses: JobApplicationStatus[] = [
    'Applied',
    'Assessment',
    'Interview',
    'Offer',
    'Rejected',
    'Withdrawn',
  ];
  readonly availableSources = signal<string[]>([]);
  readonly totalApplications = signal<number | null>(null);
  readonly matchingApplications = signal(0);
  readonly pageNumber = signal(1);
  readonly pageSize = 10;
  readonly totalPages = signal(0);
  private readonly appliedFilters = signal({
    search: '',
    status: '' as JobApplicationStatus | '',
    source: '',
  });
  readonly hasActiveFilters = computed(() =>
    Object.values(this.appliedFilters()).some((value) => value !== ''),
  );
  readonly firstVisibleApplication = computed(() =>
    this.jobApplications().length ? (this.pageNumber() - 1) * this.pageSize + 1 : 0,
  );
  readonly lastVisibleApplication = computed(() =>
    this.jobApplications().length
      ? this.firstVisibleApplication() + this.jobApplications().length - 1
      : 0,
  );
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
    this.loadRequests
      .pipe(
        // A newer search or page request cancels the older one, preventing stale results.
        switchMap((request) => {
          this.isLoading.set(true);
          this.loadError.set('');
          return this.jobApplicationService.getJobApplications(request).pipe(
            catchError(() => {
              this.loadError.set(
                'We could not load your applications. Check that the backend is running and try again.',
              );
              this.isLoading.set(false);
              return EMPTY;
            }),
          );
        }),
        takeUntilDestroyed(this.destroyReference),
      )
      .subscribe((applicationPage) => {
        this.totalApplications.set(applicationPage.totalApplications);
        this.matchingApplications.set(applicationPage.matchingApplications);
        this.availableSources.set(applicationPage.availableSources);
        this.totalPages.set(applicationPage.totalPages);
        if (applicationPage.pageNumber > 1 && applicationPage.items.length === 0) {
          this.loadJobApplications(Math.max(1, applicationPage.totalPages));
          return;
        }
        this.pageNumber.set(applicationPage.pageNumber);
        this.jobApplications.set(applicationPage.items);
        this.isLoading.set(false);
      });
    this.loadJobApplications();
  }

  loadJobApplications(pageNumber = this.pageNumber()) {
    this.loadRequests.next({ ...this.appliedFilters(), pageNumber, pageSize: this.pageSize });
  }

  applyFilters() {
    if (this.filterForm.invalid) return;
    const filters = this.filterForm.getRawValue();
    this.appliedFilters.set({ ...filters, search: filters.search.trim() });
    this.successMessage.set('');
    this.loadJobApplications(1);
  }

  clearFilters() {
    this.filterForm.reset({ search: '', status: '', source: '' });
    this.applyFilters();
  }

  changePage(pageNumber: number) {
    if (this.isLoading() || pageNumber < 1 || pageNumber > this.totalPages()) return;
    this.loadJobApplications(pageNumber);
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
        this.loadJobApplications(1);
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
