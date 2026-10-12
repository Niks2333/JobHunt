import { Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-auth-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './auth-page.component.html',
  styleUrl: './auth-page.component.css',
})
export class AuthPageComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly builder = inject(FormBuilder);
  readonly registering = this.route.snapshot.data['registering'] === true;
  readonly busy = signal(false);
  readonly error = signal('');
  readonly success = signal(
    this.route.snapshot.queryParamMap.get('registered') === '1'
      ? 'Account created. Sign in to get started.'
      : '',
  );
  readonly expired = this.route.snapshot.queryParamMap.get('expired') === '1';
  readonly form = this.builder.nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    password: [
      '',
      [
        Validators.required,
        Validators.maxLength(128),
        ...(this.registering
          ? [
              Validators.minLength(10),
              Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$/),
            ]
          : []),
      ],
    ],
    confirmPassword: [''],
  });

  async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.busy()) return;
    this.error.set('');
    this.success.set('');
    const values = this.form.getRawValue();
    if (this.registering && values.password !== values.confirmPassword) {
      this.error.set('The passwords must match.');
      return;
    }
    this.busy.set(true);
    try {
      const credentials = { email: values.email.trim(), password: values.password };
      if (this.registering) {
        await this.auth.register(credentials);
        this.form.controls.password.reset();
        this.form.controls.confirmPassword.reset();
        await this.router.navigate(['/login'], { queryParams: { registered: '1' } });
      } else {
        await this.auth.login(credentials);
        this.form.controls.password.reset();
        const target = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/';
        const safe =
          target.startsWith('/') &&
          !target.startsWith('//') &&
          !/^\/(login|register)([/?]|$)/.test(target);
        await this.router.navigateByUrl(safe ? target : '/');
      }
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        const fields = error.error?.errors as Record<string, string[]> | undefined;
        this.error.set(
          error.status === 0
            ? 'Cannot reach the server. Please try again.'
            : error.status === 429
              ? 'Too many attempts. Please wait a minute and try again.'
              : fields
                ? Object.values(fields).flat().join(' ')
                : (error.error?.message ?? 'Something went wrong. Please try again.'),
        );
      } else this.error.set('Something went wrong. Please try again.');
    } finally {
      this.busy.set(false);
    }
  }
}
