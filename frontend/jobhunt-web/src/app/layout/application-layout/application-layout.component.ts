import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../features/auth/services/auth.service';

@Component({
  selector: 'app-application-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './application-layout.component.html',
  styleUrl: './application-layout.component.css',
})
export class ApplicationLayoutComponent {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly signingOut = signal(false);
  readonly logoutError = signal('');
  async logout(): Promise<void> {
    if (this.signingOut()) return;
    this.signingOut.set(true);
    this.logoutError.set('');
    try {
      await this.auth.logout();
      await this.router.navigate(['/login']);
    } catch {
      this.logoutError.set('Could not sign out. Please try again.');
    } finally {
      this.signingOut.set(false);
    }
  }
}
