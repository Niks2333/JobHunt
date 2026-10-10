import { JobApplicationResponse } from './job-application-response.model';

export interface JobApplicationPageResponse {
  items: JobApplicationResponse[];
  totalApplications: number;
  matchingApplications: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  availableSources: string[];
}
