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
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, combineLatest, of, startWith, switchMap } from 'rxjs';
import { JobApplicationService } from '../../services/job-application.service';
import { JobApplicationDetailsResponse } from '../../models/job-application-details-response.model';
import { UpdateJobApplicationRequest } from '../../models/update-job-application-request.model';
import { JobApplicationFormComponent } from '../../components/job-application-form/job-application-form.component';
import { JobApplicationStatusFormComponent } from '../../components/job-application-status-form/job-application-status-form.component';
import { ChangeJobApplicationStatusRequest } from '../../models/change-job-application-status-request.model';
import { ApplicationInterviewsComponent } from '../../../interviews/components/application-interviews/application-interviews.component';

@Component({
  selector: 'app-job-application-details',
  imports: [DatePipe, RouterLink, JobApplicationFormComponent, JobApplicationStatusFormComponent, ApplicationInterviewsComponent],
  templateUrl: './job-application-details.component.html',
  styleUrl: './job-application-details.component.css',
})
export class JobApplicationDetailsComponent implements OnInit {
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly destroyReference = inject(DestroyRef);
  private readonly jobApplicationService = inject(JobApplicationService);
  private readonly reloadRequests = new Subject<void>();
  readonly applicationDetails = signal<JobApplicationDetailsResponse | null>(null);
  readonly isLoading = signal(true);
  readonly isNotFound = signal(false);
  readonly loadError = signal('');
  readonly isEditFormOpen = signal(false);
  readonly isStatusFormOpen = signal(false);
  readonly statusSaveError = signal('');
  readonly earliestStatusChangeDate = computed(() => {
    const details = this.applicationDetails();
    return (
      details?.statusHistory.reduce(
        (latestDate, historyEntry) =>
          historyEntry.occurredOn > latestDate ? historyEntry.occurredOn : latestDate,
        details.application.applicationDate,
      ) ?? ''
    );
  });
  readonly isSaving = signal(false);
  readonly saveError = signal('');
  readonly successMessage = signal('');
  private readonly editApplicationButton =
    viewChild<ElementRef<HTMLButtonElement>>('editApplicationButton');
  private readonly changeStatusButton =
    viewChild<ElementRef<HTMLButtonElement>>('changeStatusButton');

  ngOnInit() {
    combineLatest([this.activatedRoute.paramMap, this.reloadRequests.pipe(startWith(undefined))])
      .pipe(
        // Cancel an older request when the route changes or the user retries.
        switchMap(([routeParameters]) => {
          this.isEditFormOpen.set(false);
          this.isStatusFormOpen.set(false);
          this.applicationDetails.set(null);
          this.isLoading.set(true);
          this.isNotFound.set(false);
          this.loadError.set('');
          const identifierText = routeParameters.get('jobApplicationId') ?? '';
          const jobApplicationId = Number(identifierText);
          if (!/^[1-9]\d*$/.test(identifierText) || jobApplicationId > 2147483647) {
            this.isNotFound.set(true);
            return of(null);
          }

          return this.jobApplicationService.getJobApplicationDetails(jobApplicationId).pipe(
            catchError((httpError: HttpErrorResponse) => {
              if (httpError.status === 404) {
                this.isNotFound.set(true);
              } else {
                this.loadError.set(
                  'We could not load this application. Check the backend connection and try again.',
                );
              }
              // Catch inside switchMap so a later retry can still succeed.
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(this.destroyReference),
      )
      .subscribe((applicationDetails) => {
        this.applicationDetails.set(applicationDetails);
        this.isLoading.set(false);
      });
  }

  retryLoading() {
    this.reloadRequests.next();
  }

  openEditForm() {
    this.saveError.set('');
    this.successMessage.set('');
    this.isEditFormOpen.set(true);
  }

  closeEditForm() {
    if (this.isSaving()) return;
    this.isEditFormOpen.set(false);
    this.editApplicationButton()?.nativeElement.focus();
  }

  saveApplicationChanges(request: UpdateJobApplicationRequest) {
    const applicationDetails = this.applicationDetails();
    if (!applicationDetails || this.isSaving()) return;
    this.isSaving.set(true);
    this.saveError.set('');
    const jobApplicationId = applicationDetails.application.jobApplicationId;
    this.jobApplicationService
      .updateJobApplication(jobApplicationId, request)
      .pipe(takeUntilDestroyed(this.destroyReference))
      .subscribe({
        next: () => {
          this.isSaving.set(false);
          if (this.applicationDetails()?.application.jobApplicationId !== jobApplicationId) return;
          this.closeEditForm();
          this.successMessage.set('Application details updated.');
          this.retryLoading();
        },
        error: (httpError: HttpErrorResponse) => {
          this.isSaving.set(false);
          const validationErrors = httpError.error?.errors as Record<string, string[]> | undefined;
          this.saveError.set(
            validationErrors
              ? Object.values(validationErrors).flat().join(' ')
              : httpError.status === 404
                ? 'This application no longer exists. Your entries are still here.'
                : 'Your changes could not be saved. Your entries are still here; please try again.',
          );
        },
      });
  }

  openStatusForm() {
    if (this.isSaving()) return;
    this.statusSaveError.set('');
    this.successMessage.set('');
    this.isStatusFormOpen.set(true);
  }

  closeStatusForm() {
    if (this.isSaving()) return;
    this.isStatusFormOpen.set(false);
    this.changeStatusButton()?.nativeElement.focus();
  }

  saveStatusChange(request: ChangeJobApplicationStatusRequest) {
    const details = this.applicationDetails();
    if (!details || this.isSaving()) return;
    const jobApplicationId = details.application.jobApplicationId;
    this.isSaving.set(true);
    this.statusSaveError.set('');
    this.jobApplicationService
      .changeJobApplicationStatus(jobApplicationId, request)
      .pipe(takeUntilDestroyed(this.destroyReference))
      .subscribe({
        next: () => {
          this.isSaving.set(false);
          if (this.applicationDetails()?.application.jobApplicationId !== jobApplicationId) return;
          this.closeStatusForm();
          this.successMessage.set('Application status updated and added to history.');
          this.retryLoading();
        },
        error: (httpError: HttpErrorResponse) => {
          this.isSaving.set(false);
          if (this.applicationDetails()?.application.jobApplicationId !== jobApplicationId) return;
          const validationErrors = httpError.error?.errors as Record<string, string[]> | undefined;
          this.statusSaveError.set(
            validationErrors
              ? Object.values(validationErrors).flat().join(' ')
              : httpError.status === 404
                ? 'This application no longer exists. Your selection is still here.'
                : 'The status could not be saved. Your selection is still here; please try again.',
          );
        },
      });
  }
}
