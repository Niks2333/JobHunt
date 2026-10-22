import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { CreateJobApplicationRequest } from '../models/create-job-application-request.model';
import { JobApplicationResponse } from '../models/job-application-response.model';
import { JobApplicationDetailsResponse } from '../models/job-application-details-response.model';
import { UpdateJobApplicationRequest } from '../models/update-job-application-request.model';
import { ChangeJobApplicationStatusRequest } from '../models/change-job-application-status-request.model';
import { GetJobApplicationsRequest } from '../models/get-job-applications-request.model';
import { JobApplicationPageResponse } from '../models/job-application-page-response.model';

@Injectable({ providedIn: 'root' })
export class JobApplicationService {
  private readonly httpClient = inject(HttpClient);
  private readonly applicationsEndpoint = '/api/applications';

  getJobApplications(request: GetJobApplicationsRequest) {
    const parameters = new HttpParams()
      .set('search', request.search)
      .set('status', request.status)
      .set('source', request.source)
      .set('sortBy', request.sortBy)
      .set('pageNumber', request.pageNumber)
      .set('pageSize', request.pageSize);
    return this.httpClient.get<JobApplicationPageResponse>(this.applicationsEndpoint, {
      params: parameters,
    });
  }

  createJobApplication(request: CreateJobApplicationRequest) {
    return this.httpClient.post<JobApplicationResponse>(this.applicationsEndpoint, request);
  }

  getJobApplicationDetails(jobApplicationId: number) {
    return this.httpClient.get<JobApplicationDetailsResponse>(
      `${this.applicationsEndpoint}/${jobApplicationId}`,
    );
  }

  updateJobApplication(jobApplicationId: number, request: UpdateJobApplicationRequest) {
    return this.httpClient.put<JobApplicationResponse>(
      `${this.applicationsEndpoint}/${jobApplicationId}`,
      request,
    );
  }
  changeJobApplicationStatus(jobApplicationId: number, request: ChangeJobApplicationStatusRequest) {
    return this.httpClient.post<JobApplicationResponse>(
      `${this.applicationsEndpoint}/${jobApplicationId}/status-changes`,
      request,
    );
  }
}
