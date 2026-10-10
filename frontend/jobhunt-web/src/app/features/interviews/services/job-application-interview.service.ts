import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { JobApplicationInterviewResponse, SaveJobApplicationInterviewRequest, UpcomingInterviewsResponse } from '../models/job-application-interview.model';

@Injectable({ providedIn: 'root' })
export class JobApplicationInterviewService {
  private readonly httpClient = inject(HttpClient);

  getInterviews(jobApplicationId: number) {
    return this.httpClient.get<JobApplicationInterviewResponse[]>(`/api/applications/${jobApplicationId}/interviews`);
  }

  saveInterview(jobApplicationId: number, interviewId: number | null, request: SaveJobApplicationInterviewRequest) {
    const address = `/api/applications/${jobApplicationId}/interviews`;
    return interviewId === null
      ? this.httpClient.post<JobApplicationInterviewResponse>(address, request)
      : this.httpClient.put<JobApplicationInterviewResponse>(`${address}/${interviewId}`, request);
  }

  getUpcomingInterviews() {
    return this.httpClient.get<UpcomingInterviewsResponse>('/api/interviews/upcoming');
  }
}
