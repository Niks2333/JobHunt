import {
  Component,
  ElementRef,
  afterNextRender,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { JobApplicationStatus } from '../../models/job-application-response.model';
import { ChangeJobApplicationStatusRequest } from '../../models/change-job-application-status-request.model';

@Component({
  selector: 'app-job-application-status-form',
  imports: [ReactiveFormsModule],
  templateUrl: './job-application-status-form.component.html',
  styleUrl: './job-application-status-form.component.css',
})
export class JobApplicationStatusFormComponent {
  readonly currentStatus = input.required<JobApplicationStatus>();
  readonly earliestChangeDate = input.required<string>();
  readonly isSaving = input(false);
  readonly saveError = input('');
  readonly submitted = output<ChangeJobApplicationStatusRequest>();
  readonly cancelled = output<void>();
  readonly availableStatuses: JobApplicationStatus[] = [
    'Applied',
    'Assessment',
    'Interview',
    'Offer',
    'Rejected',
    'Withdrawn',
  ];
  private readonly formBuilder = inject(FormBuilder);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('statusDialog');
  readonly today = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Kolkata',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date());
  readonly statusForm = this.formBuilder.nonNullable.group({
    status: this.formBuilder.nonNullable.control<JobApplicationStatus | ''>(
      '',
      Validators.required,
    ),
    occurredOn: [this.today, Validators.required],
  });

  constructor() {
    afterNextRender(() => this.dialog().nativeElement.showModal());
  }

  cancelDialog(event: Event) {
    event.preventDefault();
    if (!this.isSaving()) this.cancelled.emit();
  }

  submitStatusChange() {
    if (this.isSaving()) return;
    this.statusForm.markAllAsTouched();
    const request = this.statusForm.getRawValue();
    if (request.status === this.currentStatus())
      this.statusForm.controls.status.setErrors({ unchanged: true });
    if (request.occurredOn < this.earliestChangeDate() || request.occurredOn > this.today)
      this.statusForm.controls.occurredOn.setErrors({ invalidDate: true });
    if (this.statusForm.invalid || !request.status) return;
    this.submitted.emit({ status: request.status, occurredOn: request.occurredOn });
  }
}
