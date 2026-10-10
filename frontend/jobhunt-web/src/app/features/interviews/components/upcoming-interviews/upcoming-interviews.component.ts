import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, Subject, catchError, switchMap } from 'rxjs';
import { JobApplicationInterviewService } from '../../services/job-application-interview.service';
import { UpcomingInterviewsResponse } from '../../models/job-application-interview.model';

@Component({
  selector: 'app-upcoming-interviews',
  imports: [DatePipe, RouterLink],
  templateUrl: './upcoming-interviews.component.html',
  styleUrl: './upcoming-interviews.component.css',
})
export class UpcomingInterviewsComponent implements OnInit {
  private readonly interviewService = inject(JobApplicationInterviewService);
  private readonly destroyReference = inject(DestroyRef);
  private readonly reloadRequests = new Subject<void>();
  readonly upcomingInterviews = signal<UpcomingInterviewsResponse | null>(null);
  readonly isLoading = signal(true);
  readonly loadError = signal('');

  ngOnInit() {
    this.reloadRequests.pipe(switchMap(() => {
      this.isLoading.set(true);
      this.loadError.set('');
      return this.interviewService.getUpcomingInterviews().pipe(catchError(() => {
        this.loadError.set('Upcoming interviews could not be loaded. Please try again.');
        this.isLoading.set(false);
        return EMPTY;
      }));
    }), takeUntilDestroyed(this.destroyReference)).subscribe((upcoming) => {
      this.upcomingInterviews.set(upcoming);
      this.isLoading.set(false);
    });
    this.reloadInterviews();
  }

  reloadInterviews() { this.reloadRequests.next(); }
}
