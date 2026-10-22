import { JobApplicationStatus } from './job-application-response.model';

export type ApplicationSortOrder = 'newest' | 'oldest' | 'company';

export interface GetJobApplicationsRequest {
  search: string;
  status: JobApplicationStatus | '';
  source: string;
  sortBy: ApplicationSortOrder;
  pageNumber: number;
  pageSize: number;
}
