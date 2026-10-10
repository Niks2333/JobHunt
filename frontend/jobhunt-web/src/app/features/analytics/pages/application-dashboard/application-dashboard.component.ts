import { Component, DestroyRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, Subject, catchError, switchMap } from 'rxjs';
import { ApplicationAnalyticsService } from '../../services/application-analytics.service';
import { ApplicationAnalyticsResponse } from '../../models/application-analytics-response.model';
import { GetApplicationAnalyticsRequest } from '../../models/get-application-analytics-request.model';
import { UpcomingInterviewsComponent } from '../../../interviews/components/upcoming-interviews/upcoming-interviews.component';

@Component({
  selector: 'app-application-dashboard',
  imports: [ReactiveFormsModule, RouterLink, DatePipe, DecimalPipe, UpcomingInterviewsComponent],
  templateUrl: './application-dashboard.component.html',
  styleUrl: './application-dashboard.component.css',
})
export class ApplicationDashboardComponent implements OnInit {
  private readonly analyticsService = inject(ApplicationAnalyticsService);
  private readonly destroyReference = inject(DestroyRef);
  private readonly formBuilder = inject(FormBuilder);
  private readonly loadRequests = new Subject<GetApplicationAnalyticsRequest>();
  private readonly upcomingInterviews = viewChild(UpcomingInterviewsComponent);
  private appliedFilter: GetApplicationAnalyticsRequest = { startDate: '', endDate: '' };
  readonly dateFilterForm = this.formBuilder.nonNullable.group({ startDate: [''], endDate: [''] });
  readonly analytics = signal<ApplicationAnalyticsResponse | null>(null);
  readonly isLoading = signal(true);
  readonly loadError = signal('');
  readonly filterError = signal('');
  readonly largestSourceCount = computed(() =>
    Math.max(1, ...(this.analytics()?.applicationsBySource.map((source) => source.count) ?? [])),
  );

  ngOnInit() {
    this.loadRequests.pipe(
      switchMap((request) => {
        this.isLoading.set(true);
        this.loadError.set('');
        return this.analyticsService.getApplicationAnalytics(request).pipe(catchError(() => {
          this.loadError.set('We could not load your dashboard. Check that the backend is running and try again.');
          this.isLoading.set(false);
          return EMPTY;
        }));
      }),
      takeUntilDestroyed(this.destroyReference),
    ).subscribe((analytics) => {
      this.analytics.set(analytics);
      this.isLoading.set(false);
    });
    this.refreshDashboard();
  }

  applyDateFilter() {
    const request = this.dateFilterForm.getRawValue();
    if (this.dateFilterForm.invalid ||
      (request.startDate && request.endDate && request.startDate > request.endDate)) {
      this.filterError.set('Choose valid dates with the start date on or before the end date.');
      return;
    }
    this.filterError.set('');
    this.appliedFilter = request;
    this.refreshDashboard();
  }

  clearDateFilter() {
    this.dateFilterForm.reset({ startDate: '', endDate: '' });
    this.applyDateFilter();
  }

  refreshDashboard() {
    this.loadRequests.next(this.appliedFilter);
    this.upcomingInterviews()?.reloadInterviews();
  }
}
