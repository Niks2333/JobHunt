import { DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ApplicationAnswer,
  applicationQuestions,
  JobhuntAssistantService,
} from '../../services/jobhunt-assistant.service';

interface ConversationTurn {
  question: string;
  answer: ApplicationAnswer;
}

@Component({
  selector: 'app-ask-jobhunt',
  imports: [DatePipe, RouterLink, ReactiveFormsModule],
  templateUrl: './ask-jobhunt.component.html',
  styleUrl: './ask-jobhunt.component.css',
})
export class AskJobhuntComponent {
  private readonly assistant = inject(JobhuntAssistantService);
  private readonly destroyRef = inject(DestroyRef);
  readonly questions = applicationQuestions;
  readonly turns = signal<ConversationTurn[]>([]);
  readonly pendingQuestion = signal('');
  readonly error = signal('');
  readonly failedQuestion = signal('');
  readonly questionInput = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(1000), Validators.pattern(/\S/)],
  });
  readonly questionForm = new FormGroup({ question: this.questionInput });

  submit() {
    this.questionInput.markAsTouched();
    if (this.questionInput.invalid) return;
    this.ask(this.questionInput.value);
  }

  ask(text: string) {
    if (this.pendingQuestion()) return;
    const question = text.trim();
    if (!question || question.length > 1000) return;
    this.pendingQuestion.set(question);
    this.error.set('');
    this.failedQuestion.set('');

    this.assistant
      .ask(question)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (answer) => {
          this.turns.update((turns) => [...turns, { question, answer }]);
          if (this.questionInput.value.trim() === question) this.questionInput.reset();
          this.pendingQuestion.set('');
        },
        error: (error: HttpErrorResponse) => {
          this.pendingQuestion.set('');
          this.failedQuestion.set(question);
          this.error.set(
            error.status === 429
              ? 'You have sent several questions. Please wait a minute and try again.'
              : typeof error.error?.detail === 'string'
                ? error.error.detail
                : 'Your question could not be answered. Please try again.',
          );
        },
      });
  }
}
