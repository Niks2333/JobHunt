import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { InterviewMode, JobApplicationInterviewResponse, SaveJobApplicationInterviewRequest } from '../../models/job-application-interview.model';

@Component({
  selector: 'app-interview-form',
  imports: [ReactiveFormsModule],
  templateUrl: './interview-form.component.html',
  styleUrl: './interview-form.component.css',
})
export class InterviewFormComponent implements OnInit {
  readonly initialInterview = input<JobApplicationInterviewResponse | null>(null);
  readonly applicationDate = input.required<string>();
  readonly isSaving = input(false);
  readonly saveError = input('');
  readonly submitted = output<SaveJobApplicationInterviewRequest>();
  readonly cancelled = output<void>();
  readonly validationError = signal('');
  private readonly formBuilder = inject(FormBuilder);
  readonly interviewForm = this.formBuilder.nonNullable.group({
    roundName: ['', [Validators.required, Validators.maxLength(150)]],
    scheduledLocalTime: ['', Validators.required],
    mode: this.formBuilder.nonNullable.control<InterviewMode>('Online'),
    meetingUrl: ['', Validators.maxLength(2048)],
    location: ['', Validators.maxLength(200)],
    preparationNotes: ['', Validators.maxLength(5000)],
    feedback: ['', Validators.maxLength(5000)],
    isCancelled: [false],
  });

  ngOnInit() {
    const interview = this.initialInterview();
    if (!interview) return;
    // Explicit India timezone keeps editing stable even when the browser uses another timezone.
    const dateParts = Object.fromEntries(new Intl.DateTimeFormat('en-CA', {
      timeZone: 'Asia/Kolkata', year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
    }).formatToParts(new Date(interview.scheduledAtUtc)).map((part) => [part.type, part.value]));
    const scheduledLocalTime = `${dateParts['year']}-${dateParts['month']}-${dateParts['day']}T${dateParts['hour']}:${dateParts['minute']}`;
    this.interviewForm.patchValue({
      roundName: interview.roundName, scheduledLocalTime, mode: interview.mode,
      meetingUrl: interview.meetingUrl ?? '', location: interview.location ?? '',
      preparationNotes: interview.preparationNotes ?? '', feedback: interview.feedback ?? '',
      isCancelled: interview.isCancelled,
    });
  }

  submitInterview() {
    if (this.isSaving()) return;
    this.interviewForm.markAllAsTouched();
    const fields = this.interviewForm.getRawValue();
    if (this.interviewForm.invalid || !fields.roundName.trim() ||
      fields.scheduledLocalTime.slice(0, 10) < this.applicationDate()) {
      this.validationError.set('Enter a round name and an interview date on or after the application date. Check the field lengths.');
      return;
    }
    const optionalValue = (value: string) => value.trim() || null;
    this.validationError.set('');
    this.submitted.emit({
      roundName: fields.roundName.trim(), scheduledAt: `${fields.scheduledLocalTime}+05:30`,
      mode: fields.mode, meetingUrl: optionalValue(fields.meetingUrl), location: optionalValue(fields.location),
      preparationNotes: optionalValue(fields.preparationNotes), feedback: optionalValue(fields.feedback),
      isCancelled: fields.isCancelled,
    });
  }
}
