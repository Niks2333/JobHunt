import { Component, DestroyRef, OnInit, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, Subject, catchError, switchMap } from 'rxjs';
import { JobApplicationInterviewService } from '../../services/job-application-interview.service';
import { JobApplicationInterviewResponse, SaveJobApplicationInterviewRequest } from '../../models/job-application-interview.model';
import { InterviewFormComponent } from '../interview-form/interview-form.component';

@Component({
  selector: 'app-application-interviews',
  imports: [DatePipe, InterviewFormComponent],
  templateUrl: './application-interviews.component.html',
  styleUrl: './application-interviews.component.css',
})
export class ApplicationInterviewsComponent implements OnInit {
  readonly jobApplicationId = input.required<number>();
  readonly applicationDate = input.required<string>();
  readonly interviews = signal<JobApplicationInterviewResponse[]>([]);
  readonly isLoading = signal(true);
  readonly loadError = signal('');
  readonly saveError = signal('');
  readonly successMessage = signal('');
  readonly isSaving = signal(false);
  readonly isFormOpen = signal(false);
  readonly editingInterview = signal<JobApplicationInterviewResponse | null>(null);
  private readonly interviewService = inject(JobApplicationInterviewService);
  private readonly destroyReference = inject(DestroyRef);
  private readonly reloadRequests = new Subject<void>();

  ngOnInit() {
    this.reloadRequests.pipe(switchMap(() => {
      this.isLoading.set(true);
      this.loadError.set('');
      return this.interviewService.getInterviews(this.jobApplicationId()).pipe(catchError(() => {
        this.loadError.set('Interview rounds could not be loaded. Please try again.');
        this.isLoading.set(false);
        return EMPTY;
      }));
    }), takeUntilDestroyed(this.destroyReference)).subscribe((interviews) => {
      this.interviews.set(interviews);
      this.isLoading.set(false);
    });
    this.reloadInterviews();
  }

  reloadInterviews() { this.reloadRequests.next(); }

  openForm(interview: JobApplicationInterviewResponse | null = null) {
    if (this.isSaving()) return;
    this.editingInterview.set(interview);
    this.saveError.set('');
    this.successMessage.set('');
    this.isFormOpen.set(true);
  }

  closeForm() { if (!this.isSaving()) this.isFormOpen.set(false); }

  saveInterview(request: SaveJobApplicationInterviewRequest) {
    if (this.isSaving()) return;
    this.isSaving.set(true);
    this.saveError.set('');
    this.interviewService.saveInterview(this.jobApplicationId(),
      this.editingInterview()?.jobApplicationInterviewId ?? null, request)
      .pipe(takeUntilDestroyed(this.destroyReference)).subscribe({
        next: () => {
          this.isSaving.set(false);
          this.closeForm();
          this.successMessage.set('Interview round saved. Application status is unchanged.');
          this.reloadInterviews();
        },
        error: (error: HttpErrorResponse) => {
          this.isSaving.set(false);
          const errors = error.error?.errors as Record<string, string[]> | undefined;
          this.saveError.set(errors ? Object.values(errors).flat().join(' ') : 'The round could not be saved. Your entries are still here.');
        },
      });
  }
}
