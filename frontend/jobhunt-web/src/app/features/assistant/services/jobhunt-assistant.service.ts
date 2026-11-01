import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ApplicationAnalyticsResponse } from '../../analytics/models/application-analytics-response.model';

export const applicationQuestions = [
  'How many applications do I have?',
  'How many applications are in each status?',
  'Which source do I apply through most?',
  'What is my interview conversion rate?',
  'How many applications did I make yesterday?',
  'What should I revise about SQL based on my interview feedback?',
  'What does my feedback say about Angular?',
];

export interface AssistantSource {
  id: string;
  jobApplicationId: number;
  interviewId: number | null;
  title: string;
  kind: string;
  chunkNumber: number;
  passage: string;
}

export interface ApplicationAnswer {
  text: string;
  toolUsed: string;
  retrievedAtUtc: string;
  analytics: ApplicationAnalyticsResponse | null;
  retrieval: {
    query: string;
    documentsSearched: number;
    chunksSearched: number;
    sources: AssistantSource[];
  } | null;
}

@Injectable({ providedIn: 'root' })
export class JobhuntAssistantService {
  private readonly http = inject(HttpClient);

  ask(question: string) {
    return this.http.post<ApplicationAnswer>('/api/assistant/ask', { question });
  }
}
