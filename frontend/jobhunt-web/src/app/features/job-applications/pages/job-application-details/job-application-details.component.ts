import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, combineLatest, of, startWith, switchMap } from 'rxjs';
import { JobApplicationService } from '../../services/job-application.service';
import { JobApplicationDetailsResponse } from '../../models/job-application-details-response.model';

@Component({
  selector: 'app-job-application-details',
  imports: [DatePipe, RouterLink],
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

  ngOnInit() {
    combineLatest([this.activatedRoute.paramMap, this.reloadRequests.pipe(startWith(undefined))])
      .pipe(
        // Cancel an older request when the route changes or the user retries.
        switchMap(([routeParameters]) => {
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
}
