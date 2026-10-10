import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CreateJobApplicationRequest } from '../models/create-job-application-request.model';
import { JobApplicationResponse } from '../models/job-application-response.model';
import { JobApplicationDetailsResponse } from '../models/job-application-details-response.model';

@Injectable({ providedIn: 'root' })
export class JobApplicationService {
  private readonly httpClient = inject(HttpClient);
  private readonly applicationsEndpoint = '/api/applications';

  getJobApplications() {
    return this.httpClient.get<JobApplicationResponse[]>(this.applicationsEndpoint);
  }

  createJobApplication(request: CreateJobApplicationRequest) {
    return this.httpClient.post<JobApplicationResponse>(this.applicationsEndpoint, request);
  }

  getJobApplicationDetails(jobApplicationId: number) {
    return this.httpClient.get<JobApplicationDetailsResponse>(
      `${this.applicationsEndpoint}/${jobApplicationId}`,
    );
  }
}
