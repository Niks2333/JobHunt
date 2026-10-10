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
import { CreateJobApplicationRequest } from '../../models/create-job-application-request.model';

@Component({
  selector: 'app-job-application-form',
  imports: [ReactiveFormsModule],
  templateUrl: './job-application-form.component.html',
  styleUrl: './job-application-form.component.css',
})
export class JobApplicationFormComponent {
  readonly isSaving = input(false);
  readonly saveError = input('');
  readonly submitted = output<CreateJobApplicationRequest>();
  readonly cancelled = output<void>();
  private readonly formBuilder = inject(FormBuilder);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('applicationDialog');
  readonly today = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Kolkata',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date());

  readonly applicationForm = this.formBuilder.nonNullable.group({
    companyName: ['', [Validators.required, Validators.maxLength(150), Validators.pattern(/\S/)]],
    roleTitle: ['', [Validators.required, Validators.maxLength(150), Validators.pattern(/\S/)]],
    location: ['', Validators.maxLength(150)],
    source: ['', Validators.maxLength(100)],
    applicationDate: [this.today, Validators.required],
    jobUrl: ['', [Validators.maxLength(2048), Validators.pattern(/^https?:\/\/\S+$/i)]],
    notes: [''],
  });

  constructor() {
    // Native dialogs contain keyboard focus and support Escape without another library.
    afterNextRender(() => this.dialog().nativeElement.showModal());
  }

  cancelDialog(event: Event) {
    event.preventDefault();
    if (!this.isSaving()) this.cancelled.emit();
  }

  submitApplication() {
    if (this.isSaving()) return;
    this.applicationForm.markAllAsTouched();
    const values = this.applicationForm.getRawValue();
    if (values.applicationDate > this.today) {
      this.applicationForm.controls.applicationDate.setErrors({ futureDate: true });
    }
    if (this.applicationForm.invalid) return;

    this.submitted.emit({
      companyName: values.companyName.trim(),
      roleTitle: values.roleTitle.trim(),
      applicationDate: values.applicationDate,
      location: values.location.trim() || null,
      source: values.source.trim() || null,
      jobUrl: values.jobUrl.trim() || null,
      notes: values.notes.trim() || null,
    });
  }
}
