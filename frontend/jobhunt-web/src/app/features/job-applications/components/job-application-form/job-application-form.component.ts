import {
  Component,
  ElementRef,
  OnInit,
  afterNextRender,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { JobApplicationFieldsRequest } from '../../models/job-application-fields-request.model';
import { JobApplicationResponse } from '../../models/job-application-response.model';

@Component({
  selector: 'app-job-application-form',
  imports: [ReactiveFormsModule],
  templateUrl: './job-application-form.component.html',
  styleUrl: './job-application-form.component.css',
})
export class JobApplicationFormComponent implements OnInit {
  readonly initialApplication = input<JobApplicationResponse | null>(null);
  readonly isSaving = input(false);
  readonly saveError = input('');
  readonly submitted = output<JobApplicationFieldsRequest>();
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

  ngOnInit() {
    const application = this.initialApplication();
    if (application) {
      this.applicationForm.setValue({
        companyName: application.companyName,
        roleTitle: application.roleTitle,
        applicationDate: application.applicationDate,
        location: application.location ?? '',
        source: application.source ?? '',
        jobUrl: application.jobUrl ?? '',
        notes: application.notes ?? '',
      });
    }
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
